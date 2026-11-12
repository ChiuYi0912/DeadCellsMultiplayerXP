using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using Serilog;
using LevelMob = dc.level.Mob;
using Math = System.Math;

using DeadCellsMultiplayerX.Engine.Utilities;
namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication
{
    /// <summary>
    /// Mob 类型的客户端处理器
    /// </summary>
    internal sealed class ClientMobReplicator : IClientEntityKindHandler
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientMobReplicator");

        private readonly GuestClientSession session;
        private readonly ClientMobModifierRegistry mobModifiers;
        private readonly WorldFrameTimeline frames;
        private readonly ClientEntityStore store;
        private readonly ClientMobFrameReceiver receiver;
        private readonly ClientMobAffectState affectState;

        private readonly HashSet<LevelKey> authoritativeLevels = [];

        private bool loggedFirstMob;

        public ClientMobReplicator(
            GuestClientSession session,
            ClientMobModifierRegistry mobModifiers,
            WorldFrameTimeline frames,
            ClientEntityStore store)
        {
            this.session = session;
            this.mobModifiers = mobModifiers;
            this.frames = frames;
            this.store = store;
            receiver = new ClientMobFrameReceiver(store);
            affectState = new ClientMobAffectState();
        }

        public ClientMobFrameReceiver Receiver => receiver;

        public EntityKind Kind => EntityKind.Mob;

        public bool IsRemoteMob(Mob mob) => receiver.IsRemoteMob(mob);

        public bool IsDeathSuppressed(Mob mob) => receiver.IsDeathSuppressed(mob);

        /// <inheritdoc />
        public bool TryCreate(
            EntitySpawn spawn, out object native)
        {
            native = null!;

            if (spawn == null
                || spawn.EntityData == null
                || spawn.EntityData.Length == 0)
                return false;

            var level = LevelUtils.ResolveLevel(
                session.Game, spawn.LevelId, spawn.SubLevelIndex);
            if (level == null) return false;

            LevelKey levelKey = LevelKey.Make(spawn.LevelId, spawn.SubLevelIndex);
            bool needsCleanup = !authoritativeLevels.Contains(levelKey);

            if (needsCleanup)
                ClearLocalLevelMobs(level);

            LevelMob? descriptor =
                DCMXSerializers.MessagePack.Deserialize<LevelMob>(spawn.EntityData);

            if (descriptor == null) return false;

            Mob? mob = level.attachMob(descriptor);
            if (mob == null) return false;

            //mob.hasRepelling = false;
            mob.hasGravity = false;
            mob.gravity = 0;
            mob.collisionMode = new CollisionMode.None();
            mob.frict = 0;

            if (needsCleanup)
                authoritativeLevels.Add(levelKey);

            Logger.Verbose(
                "Mob spawned on client: netId={NetId} level={LevelId}#{SubLevelIndex}",
                spawn.NetId, spawn.LevelId, spawn.SubLevelIndex);

            native = mob;
            return true;
        }

        /// <inheritdoc />
        public void OnCreated(EntitySpawn spawn, object native)
        {
            if (native is not Mob mob)
                return;

            if (!mobModifiers.TryAttach(mob, session.SyncedTimeMs))
                return;

            mobModifiers.DispatchInitSkills(
                mob, mob.oldSkills, mob.skills, session.SyncedTimeMs);
        }

        /// <inheritdoc />
        public void Despawn(
            uint netId, object? native,
            EntityDespawnReason reason)
        {
            session.Prediction.ConfirmDespawn(netId);

            if (native is Mob mob)
            {
                mobModifiers.Detach(mob);

                if (!receiver.IsDeathSuppressed(mob))
                {
                    // 关卡卸载/对账移除只销毁，不触发本地死亡掉落
                    if (reason == EntityDespawnReason.LevelUnloaded)
                    {
                        if (!mob.destroyed)
                            mob.destroy();
                    }
                    else
                    {
                        // 服务端确认死亡
                        session.ForceNativeDeath(mob);
                    }
                }
            }

            affectState.Remove(netId);
            session.Prediction.OnRemoved(netId);
        }

        /// <inheritdoc />
        public void BeforeSpawns(WorldFrame frame)
        {
            // 必须在移除之前确认，避免预测死亡的 mob 已被移除
            session.Prediction.ConfirmFrame(frame);
        }

        /// <inheritdoc />
        public void AfterSpawns(WorldFrame frame)
        {
            if (frame.Entities == null) return;

            // 服务端帧已给出 Life<=0 但还没进 DespawnedEntities，补充死亡
            ForceFrameDeadMobs(frame);

            // 本地预测致死后服务端仍存活，使用 NetId 重建
            RepairDestroyedMobs(frame);

            // 有动态状态但没有本地实体也没有待生成数据时请求重同步
            foreach (var dynamic in frame.Entities)
            {
                if (dynamic == null || dynamic.Kind != EntityKind.Mob) continue;
                if (!store.HasOrPending(dynamic.NetId))
                    frames.RequestResync("mob spawn missing");
            }
        }

        /// <inheritdoc />
        public void AfterFrame(WorldFrame frame)
        {
            if (!loggedFirstMob && frame.Entities != null)
            {
                int count = 0;
                foreach (var dynamic in frame.Entities)
                {
                    if (dynamic != null && dynamic.Kind == EntityKind.Mob)
                        count++;
                }

                if (count > 0)
                {
                    loggedFirstMob = true;
                    Logger.Information(
                        "First mob frame: epoch={Epoch} seq={Sequence} mobs={Mobs}",
                        frame.Epoch, frame.Sequence, count);
                }
            }
        }

        /// <inheritdoc />
        public void Tick(long nowMs)
        {
            session.Prediction.Tick(nowMs);

            double nowSec = session.SyncedTimeMs / 1000.0;

            foreach (var mob in receiver.All)
            {
                if (mob == null || mob.destroyed) continue;
                if (!receiver.TryGetNetId(mob, out var netId)) continue;

                if (!frames.TryGetEntityBracket(
                        netId, nowSec,
                        out var prev, out var next,
                        out double alpha, out double renderTimeSec))
                {
                    affectState.ExpireIfDue(netId, mob, nowSec);
                    continue;
                }

                var sample = PoseInterpolator.Interpolate(
                    prev.Pos, next.Pos, alpha);

                mob.setPosCase(
                    sample.Cx, sample.Cy, sample.Xr, sample.Yr);
                mob.dir = sample.Dir;
                mob.dx = 0;
                mob.dy = 0;
                mob.bdx = 0;
                mob.bdy = 0;

                if (!mobModifiers.DispatchClientUpdate(mob, nowMs))
                    RemoteMobAnimator.Apply(mob, prev.Anim, next.Anim, renderTimeSec);

                affectState.ApplySnapshot(
                    netId, mob, next.Affects ?? prev.Affects, nowSec);

                int serverLife = (int)Math.Round(
                    prev.Life + (next.Life - prev.Life) * alpha);
                long renderTimeMs = (long)Math.Round(renderTimeSec * 1000);

                int life = session.Prediction.ResolveLife(
                    netId, serverLife, renderTimeMs);

                mob.life = life;
                mob.maxLife = next.MaxLife;
            }
        }

        /// <inheritdoc />
        public void OnEpochReset()
        {
            session.Prediction.Clear();

            // 原生 Mob 的统一销毁由 ClientEntityStore.Clear → Despawn 负责。
            affectState.Reset();
            authoritativeLevels.Clear();
            receiver.ClearSuppressed();
        }

        /// <summary>
        /// 执行由服务端发送的 mob 死亡指令
        /// </summary>
        private void ForceFrameDeadMobs(WorldFrame frame)
        {
            foreach (var dynamic in frame.Entities)
            {
                if (dynamic == null
                    || dynamic.Kind != EntityKind.Mob
                    || dynamic.Life > 0)
                    continue;

                if (receiver.Get(dynamic.NetId) is not { destroyed: false } mob)
                    continue;

                session.Prediction.ConfirmDespawn(dynamic.NetId);
                session.ForceNativeDeath(mob);
            }
        }

        /// <summary>
        /// 预测错误时重建对应 mob
        /// </summary>
        private void RepairDestroyedMobs(WorldFrame frame)
        {
            foreach (var dynamic in frame.Entities)
            {
                if (dynamic == null
                    || dynamic.Kind != EntityKind.Mob
                    || dynamic.Life <= 0)
                    continue;

                // 本地预测致死还未确认前，服务端旧帧仍可能显示存活
                if (session.Prediction.HasPendingDeathPrediction(dynamic.NetId))
                    continue;

                if (!store.IsMissingOrDestroyed(dynamic.NetId)) continue;

                if (store.TryRecreate(dynamic.NetId)
                    && store.Get(dynamic.NetId) is Mob mob)
                {
                    ApplyDynamicNow(dynamic, mob);
                    continue;
                }

                // 重建失败但旧实体已销毁，清掉旧映射，触发后续 resync 恢复
                if (store.Get(dynamic.NetId) is Entity { destroyed: true })
                    store.Remove(dynamic.NetId, EntityDespawnReason.Removed);
            }
        }

        private void ApplyDynamicNow(EntityDynamic dynamic, Mob mob)
        {
            PoseInterpolator.SetPosition(
                mob,
                dynamic.Pos.CX + dynamic.Pos.XR,
                dynamic.Pos.CY + dynamic.Pos.XY);
            mob.dir = dynamic.Pos.DIR;
            mob.dx = dynamic.Pos.DX;
            mob.dy = dynamic.Pos.DY;
            mob.bdx = dynamic.Pos.BDX;
            mob.bdy = dynamic.Pos.BDY;

            mob.life = dynamic.Life;
            mob.maxLife = dynamic.MaxLife;

            double nowSec = session.SyncedTimeMs / 1000.0;
            if (!mobModifiers.DispatchClientUpdate(
                    mob, session.SyncedTimeMs))
            {
                RemoteMobAnimator.Apply(mob, dynamic.Anim, dynamic.Anim, nowSec);
            }

            affectState.ApplySnapshot(
                dynamic.NetId, mob, dynamic.Affects, nowSec);

            session.Prediction.ClearNetId(dynamic.NetId);
        }

        /// <inheritdoc />
        public void OnEntityEvent(EntityEvent evt)
        {
            if (evt is not MobModifierEvent mobEvt)
                return;

            var mob = receiver.Get(mobEvt.NetId);
            if (mob == null || mob.destroyed)
                return;

            mobModifiers.DispatchClientEvent(
                mob, mobEvt, session.SyncedTimeMs);
        }

        /// <summary>
        /// 清掉指定关卡客户端本地加载的怪物
        /// </summary>
        private void ClearLocalLevelMobs(Level level)
        {
            var entities = level.entities;
            if (entities == null) return;

            var toDestroy = new List<Mob>();
            for (int i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Mob mob
                    && !receiver.IsRemoteMob(mob)
                    && !mob.destroyed)
                {
                    toDestroy.Add(mob);
                }
            }

            // 清理本地怪物时同样抑制本地死亡掉落
            foreach (var mob in toDestroy)
                receiver.SuppressDeath(mob);

            foreach (var mob in toDestroy)
                mob.destroy();

            foreach (var mob in toDestroy)
                receiver.UnsuppressDeath(mob);
        }

        public void Clear()
        {
            OnEpochReset();
        }
    }
}
