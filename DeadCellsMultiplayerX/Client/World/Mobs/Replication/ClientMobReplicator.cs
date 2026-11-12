namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;
using LevelMob = dc.level.Mob;
using Math = Math;

/// <summary>
/// Mob 类型的客户端处理器
/// </summary>
internal sealed class ClientMobReplicator(
    GuestClientSession session,
    ClientMobModifierRegistry mobModifiers,
    WorldFrameTimeline frames,
    ClientEntityStore store) : IClientEntityKindHandler
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientMobReplicator");

    private readonly GuestClientSession session = session;
    private readonly ClientMobModifierRegistry mobModifiers = mobModifiers;
    private readonly WorldFrameTimeline frames = frames;
    private readonly ClientEntityStore store = store;
    private readonly ClientMobAffectState affectState = new ClientMobAffectState();

    private readonly HashSet<LevelKey> authoritativeLevels = [];

    private bool loggedFirstMob;

    public ClientMobFrameReceiver Receiver { get; } = new ClientMobFrameReceiver(store);

    public EntityKind Kind => EntityKind.Mob;

    public bool IsRemoteMob(Mob mob) => this.Receiver.IsRemoteMob(mob);

    public bool IsDeathSuppressed(Mob mob) => this.Receiver.IsDeathSuppressed(mob);

    /// <inheritdoc />
    public bool TryCreate(
        EntitySpawn spawn, out object native)
    {
        native = null!;

        if (spawn == null
            || spawn.EntityData == null
            || spawn.EntityData.Length == 0)
        {
            return false;
        }

        var level = LevelUtils.ResolveLevel(
            GuestClientSession.Game, spawn.LevelId, spawn.SubLevelIndex);
        if (level == null)
        {
            return false;
        }

        LevelKey levelKey = LevelKey.Make(spawn.LevelId, spawn.SubLevelIndex);
        var needsCleanup = !this.authoritativeLevels.Contains(levelKey);

        if (needsCleanup)
        {
            this.ClearLocalLevelMobs(level);
        }

        LevelMob? descriptor =
            DCMXSerializers.MessagePack.Deserialize<LevelMob>(spawn.EntityData);

        if (descriptor == null)
        {
            return false;
        }

        Mob? mob = level.attachMob(descriptor);
        if (mob == null)
        {
            return false;
        }

        //mob.hasRepelling = false;
        mob.hasGravity = false;
        mob.gravity = 0;
        mob.collisionMode = new CollisionMode.None();
        mob.frict = 0;

        if (needsCleanup)
        {
            this.authoritativeLevels.Add(levelKey);
        }

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
        {
            return;
        }

        if (!this.mobModifiers.TryAttach(mob, this.session.SyncedTimeMs))
        {
            return;
        }

        this.mobModifiers.DispatchInitSkills(
            mob, mob.oldSkills, mob.skills, this.session.SyncedTimeMs);
    }

    /// <inheritdoc />
    public void Despawn(
        uint netId, object? native,
        EntityDespawnReason reason)
    {
        this.session.Prediction.ConfirmDespawn(netId);

        if (native is Mob mob)
        {
            this.mobModifiers.Detach(mob);

            if (!this.Receiver.IsDeathSuppressed(mob))
            {
                // 关卡卸载/对账移除只销毁，不触发本地死亡掉落
                if (reason == EntityDespawnReason.LevelUnloaded)
                {
                    if (!mob.destroyed)
                    {
                        mob.destroy();
                    }
                }
                else
                {
                    // 服务端确认死亡
                    this.session.ForceNativeDeath(mob);
                }
            }
        }

        this.affectState.Remove(netId);
        this.session.Prediction.OnRemoved(netId);
    }

    /// <inheritdoc />
    public void BeforeSpawns(WorldFrame frame) =>
        // 必须在移除之前确认，避免预测死亡的 mob 已被移除
        this.session.Prediction.ConfirmFrame(frame);

    /// <inheritdoc />
    public void AfterSpawns(WorldFrame frame)
    {
        if (frame.Entities == null)
        {
            return;
        }

        // 服务端帧已给出 Life<=0 但还没进 DespawnedEntities，补充死亡
        this.ForceFrameDeadMobs(frame);

        // 本地预测致死后服务端仍存活，使用 NetId 重建
        this.RepairDestroyedMobs(frame);

        // 有动态状态但没有本地实体也没有待生成数据时请求重同步
        foreach (var dynamic in frame.Entities)
        {
            if (dynamic == null || dynamic.Kind != EntityKind.Mob)
            {
                continue;
            }

            if (!this.store.HasOrPending(dynamic.NetId))
            {
                this.frames.RequestResync("mob spawn missing");
            }
        }
    }

    /// <inheritdoc />
    public void AfterFrame(WorldFrame frame)
    {
        if (!this.loggedFirstMob && frame.Entities != null)
        {
            var count = 0;
            foreach (var dynamic in frame.Entities)
            {
                if (dynamic != null && dynamic.Kind == EntityKind.Mob)
                {
                    count++;
                }
            }

            if (count > 0)
            {
                this.loggedFirstMob = true;
                Logger.Information(
                    "First mob frame: epoch={Epoch} seq={Sequence} mobs={Mobs}",
                    frame.Epoch, frame.Sequence, count);
            }
        }
    }

    /// <inheritdoc />
    public void Tick(long nowMs)
    {
        this.session.Prediction.Tick(nowMs);

        var nowSec = this.session.SyncedTimeMs / 1000.0;

        foreach (var mob in this.Receiver.All)
        {
            if (mob == null || mob.destroyed)
            {
                continue;
            }

            if (!this.Receiver.TryGetNetId(mob, out var netId))
            {
                continue;
            }

            if (!this.frames.TryGetEntityBracket(
                    netId, nowSec,
                    out var prev, out var next,
                    out var alpha, out var renderTimeSec))
            {
                this.affectState.ExpireIfDue(netId, mob, nowSec);
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

            if (!this.mobModifiers.DispatchClientUpdate(mob, nowMs))
            {
                RemoteMobAnimator.Apply(mob, prev.Anim, next.Anim, renderTimeSec);
            }

            this.affectState.ApplySnapshot(
                netId, mob, next.Affects ?? prev.Affects, nowSec);

            var serverLife = (int)Math.Round(
                prev.Life + ((next.Life - prev.Life) * alpha));
            var renderTimeMs = (long)Math.Round(renderTimeSec * 1000);

            var life = this.session.Prediction.ResolveLife(
                netId, serverLife, renderTimeMs);

            mob.life = life;
            mob.maxLife = next.MaxLife;
        }
    }

    /// <inheritdoc />
    public void OnEpochReset()
    {
        this.session.Prediction.Clear();

        // 原生 Mob 的统一销毁由 ClientEntityStore.Clear → Despawn 负责。
        this.affectState.Reset();
        this.authoritativeLevels.Clear();
        this.Receiver.ClearSuppressed();
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
            {
                continue;
            }

            if (this.Receiver.Get(dynamic.NetId) is not { destroyed: false } mob)
            {
                continue;
            }

            this.session.Prediction.ConfirmDespawn(dynamic.NetId);
            this.session.ForceNativeDeath(mob);
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
            {
                continue;
            }

            // 本地预测致死还未确认前，服务端旧帧仍可能显示存活
            if (this.session.Prediction.HasPendingDeathPrediction(dynamic.NetId))
            {
                continue;
            }

            if (!this.store.IsMissingOrDestroyed(dynamic.NetId))
            {
                continue;
            }

            if (this.store.TryRecreate(dynamic.NetId)
                && this.store.Get(dynamic.NetId) is Mob mob)
            {
                this.ApplyDynamicNow(dynamic, mob);
                continue;
            }

            // 重建失败但旧实体已销毁，清掉旧映射，触发后续 resync 恢复
            if (this.store.Get(dynamic.NetId) is Entity { destroyed: true })
            {
                this.store.Remove(dynamic.NetId, EntityDespawnReason.Removed);
            }
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

        var nowSec = this.session.SyncedTimeMs / 1000.0;
        if (!this.mobModifiers.DispatchClientUpdate(
                mob, this.session.SyncedTimeMs))
        {
            RemoteMobAnimator.Apply(mob, dynamic.Anim, dynamic.Anim, nowSec);
        }

        this.affectState.ApplySnapshot(
            dynamic.NetId, mob, dynamic.Affects, nowSec);

        this.session.Prediction.ClearNetId(dynamic.NetId);
    }

    /// <inheritdoc />
    public void OnEntityEvent(EntityEvent evt)
    {
        if (evt is not MobModifierEvent mobEvt)
        {
            return;
        }

        var mob = this.Receiver.Get(mobEvt.NetId);
        if (mob == null || mob.destroyed)
        {
            return;
        }

        this.mobModifiers.DispatchClientEvent(
            mob, mobEvt, this.session.SyncedTimeMs);
    }

    /// <summary>
    /// 清掉指定关卡客户端本地加载的怪物
    /// </summary>
    private void ClearLocalLevelMobs(Level level)
    {
        var entities = level.entities;
        if (entities == null)
        {
            return;
        }

        var toDestroy = new List<Mob>();
        for (var i = 0; i < entities.length; i++)
        {
            if (entities.getDyn(i) is Mob mob
                && !this.Receiver.IsRemoteMob(mob)
                && !mob.destroyed)
            {
                toDestroy.Add(mob);
            }
        }

        // 清理本地怪物时同样抑制本地死亡掉落
        foreach (var mob in toDestroy)
        {
            this.Receiver.SuppressDeath(mob);
        }

        foreach (var mob in toDestroy)
        {
            mob.destroy();
        }

        foreach (var mob in toDestroy)
        {
            this.Receiver.UnsuppressDeath(mob);
        }
    }

    public void Clear() => this.OnEpochReset();
}
