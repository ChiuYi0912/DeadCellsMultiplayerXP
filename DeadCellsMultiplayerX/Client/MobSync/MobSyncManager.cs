using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Client.Replication.Entities;
using DeadCellsMultiplayerX.Client.Snapshot;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using Serilog;
using LevelMob = dc.level.Mob;
using Math = System.Math;

namespace DeadCellsMultiplayerX.Client.MobSync
{
    /// <summary>
    /// Mob 类型的客户端处理器
    /// </summary>
    internal sealed class MobSyncManager : IClientEntityKindHandler
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobSyncManager");

        private const long StepDriveIntervalMs = 50;

        private readonly GuestClientSession session;
        private readonly RemoteWorld remoteWorld;
        private readonly WorldFrameManager frames;
        private readonly ClientEntityStore store;
        private readonly ClientMobReceiver receiver;
        private readonly MobAffectStateManager affectState;

        private readonly HashSet<string> authoritativeLevels = [];
        private readonly Dictionary<uint, long> lastStepMs = [];

        private bool loggedFirstMob;

        public MobSyncManager(
            GuestClientSession session,
            RemoteWorld remoteWorld,
            WorldFrameManager frames,
            ClientEntityStore store)
        {
            this.session = session;
            this.remoteWorld = remoteWorld;
            this.frames = frames;
            this.store = store;
            receiver = new ClientMobReceiver(store);
            affectState = new MobAffectStateManager();
        }

        public ClientMobReceiver Receiver => receiver;

        public EntityKind Kind => EntityKind.Mob;

        public bool IsRemoteMob(Mob mob) => receiver.IsRemoteMob(mob);

        public bool IsDeathSuppressed(Mob mob) => receiver.IsDeathSuppressed(mob);

        /// <inheritdoc />
        public bool TryCreate(
            EntitySpawn spawn, ClientEntityContext ctx, out object native)
        {
            native = null!;

            if (spawn == null
                || spawn.EntityData == null
                || spawn.EntityData.Length == 0)
                return false;

            var level = LevelUtils.ResolveLevel(
                session.Game, spawn.LevelId, spawn.SubLevelIndex);
            if (level == null) return false;

            string levelKey = LevelKeys.Make(spawn.LevelId, spawn.SubLevelIndex);
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
        public void OnCreated(
            EntitySpawn spawn, object native, ClientEntityContext ctx)
        {
            if (native is not Mob mob)
                return;

            if (!remoteWorld.MobModifiers.TryAttach(mob, session.SyncedTimeMs))
                return;

            remoteWorld.MobModifiers.DispatchInitSkills(
                mob, mob.oldSkills, mob.skills, session.SyncedTimeMs);
        }

        /// <inheritdoc />
        public void Despawn(
            uint netId, object? native,
            EntityDespawnReason reason, ClientEntityContext ctx)
        {
            session.Prediction.ConfirmDespawn(netId);

            if (native is Mob mob)
            {
                remoteWorld.MobModifiers.Detach(mob);

                // 服务端确认死亡
                if (!receiver.IsDeathSuppressed(mob))
                    session.ForceNativeDeath(mob);
            }

            affectState.Remove(netId);
            lastStepMs.Remove(netId);
            session.Prediction.OnRemoved(netId);
        }

        /// <inheritdoc />
        public void BeforeSpawns(WorldFrame frame, ClientEntityContext ctx)
        {
            // 必须在移除之前确认，避免预测死亡的 mob 已被移除
            session.Prediction.ConfirmFrame(frame);
        }

        /// <inheritdoc />
        public void AfterSpawns(WorldFrame frame, ClientEntityContext ctx)
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
        public void AfterFrame(WorldFrame frame, ClientEntityContext ctx)
        {
            TickMobEvents(frame);

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
        public void Tick(ClientEntityContext ctx, long nowMs)
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

                double prevX = prev.Pos.CX + prev.Pos.XR;
                double prevY = prev.Pos.CY + prev.Pos.XY;
                double nextX = next.Pos.CX + next.Pos.XR;
                double nextY = next.Pos.CY + next.Pos.XY;

                double x = prevX + (nextX - prevX) * alpha;
                double y = prevY + (nextY - prevY) * alpha;

                int cx = (int)Math.Floor(x);
                int cy = (int)Math.Floor(y);

                int prevCx = mob.cx;
                int prevCy = mob.cy;

                mob.setPosCase(cx, cy, x - cx, y - cy);
                mob.dir = alpha < 0.5 ? prev.Pos.DIR : next.Pos.DIR;
                mob.dx = 0;
                mob.dy = 0;
                mob.bdx = 0;
                mob.bdy = 0;

                // 快照传送绕过了 Entity.updatePositionXY 的跨格检测，
                // 这里补一次原版 onStep，让各 mob 的重写逻辑生效
                if (mob.cx != prevCx || mob.cy != prevCy)
                    DriveStep(netId, mob, nowMs);

                if (!remoteWorld.MobModifiers.DispatchClientUpdate(mob, nowMs))
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
        public void OnEpochReset(ClientEntityContext ctx)
        {
            session.Prediction.Clear();

            // 先销毁原生实体，store 随后清空映射
            foreach (var mob in receiver.All)
            {
                if (mob == null) continue;

                remoteWorld.MobModifiers.Detach(mob);

                if (!mob.destroyed)
                    mob.destroy();
            }

            affectState.Reset();
            lastStepMs.Clear();
            authoritativeLevels.Clear();
            receiver.ClearSuppressed();
        }

        /// <summary>
        /// 快照跨格时补调原版 onStep
        /// </summary>
        private void DriveStep(uint netId, Mob mob, long nowMs)
        {
            if (lastStepMs.TryGetValue(netId, out long last)
                && nowMs - last < StepDriveIntervalMs)
                return;

            lastStepMs[netId] = nowMs;
            mob.onStep();
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
            double x = dynamic.Pos.CX + dynamic.Pos.XR;
            double y = dynamic.Pos.CY + dynamic.Pos.XY;

            int cx = (int)Math.Floor(x);
            int cy = (int)Math.Floor(y);

            mob.setPosCase(cx, cy, x - cx, y - cy);
            mob.dir = dynamic.Pos.DIR;
            mob.dx = dynamic.Pos.DX;
            mob.dy = dynamic.Pos.DY;
            mob.bdx = dynamic.Pos.BDX;
            mob.bdy = dynamic.Pos.BDY;

            mob.life = dynamic.Life;
            mob.maxLife = dynamic.MaxLife;

            double nowSec = session.SyncedTimeMs / 1000.0;
            if (!remoteWorld.MobModifiers.DispatchClientUpdate(
                    mob, session.SyncedTimeMs))
            {
                RemoteMobAnimator.Apply(mob, dynamic.Anim, dynamic.Anim, nowSec);
            }

            affectState.ApplySnapshot(
                dynamic.NetId, mob, dynamic.Affects, nowSec);

            session.Prediction.ClearNetId(dynamic.NetId);
        }

        /// <summary>
        /// 分发服务端路由出来的 mob 事件。
        /// </summary>
        private void TickMobEvents(WorldFrame frame)
        {
            if (frame?.EntityEvents == null || frame.EntityEvents.Count == 0)
                return;

            foreach (var evt in frame.EntityEvents)
            {
                if (evt == null
                    || evt.Epoch != frame.Epoch
                    || evt.EntityKind != EntityKind.Mob)
                    continue;

                var mob = receiver.Get(evt.MobNetId);
                if (mob == null || mob.destroyed)
                    continue;

                remoteWorld.MobModifiers.DispatchClientEvent(
                    mob, evt, session.SyncedTimeMs);
            }
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
            OnEpochReset(store.Context);
        }
    }
}
