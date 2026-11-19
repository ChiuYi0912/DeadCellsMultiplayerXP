namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Serialization;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;
using LevelMob = dc.level.Mob;

/// <summary>
/// Mob 类型的客户端处理器
/// </summary>
internal sealed class ClientMobReplicator(
    GuestGameSession session,
    ClientMobModifierRegistry mobModifiers,
    WorldFrameHistory history,
    ClientEntityStore store,
    Action<string> requestResync) : IClientEntityKindHandler
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientMobReplicator");

    private readonly GuestGameSession session = session;
    private readonly GuestCombatPresentation combat = session.Combat;
    private readonly ClientMobModifierRegistry mobModifiers = mobModifiers;
    private readonly Action<string> requestResync = requestResync;
    private readonly ClientEntityStore store = store;
    private readonly ClientMobStateApplier states = new(session, mobModifiers, history);
    private readonly HashSet<Mob> suppressedDeaths = [with(ReferenceEqualityComparer.Instance)];

    private readonly HashSet<LevelKey> authoritativeLevels = [];
    // A pending spawn must not create an actor after an authoritative zero-life state.
    private readonly HashSet<uint> confirmedDead = [];

    private bool loggedFirstMob;

    public IEnumerable<Mob> All => this.store.Records
        .Where(record => record.Kind == EntityKind.Mob).Select(record => record.Native).OfType<Mob>();

    public EntityKind Kind => EntityKind.Mob;

    public bool IsRemoteMob(Mob mob) => mob != null && this.store.IsReplicated(mob);

    public bool IsDeathSuppressed(Mob mob) => mob != null && this.suppressedDeaths.Contains(mob);

    /// <inheritdoc />
    public bool TryCreate(
        EntitySpawn spawn, out object native)
    {
        native = null!;

        if (spawn == null
            || this.confirmedDead.Contains(spawn.NetId)
            || spawn.EntityData == null
            || spawn.EntityData.Length == 0)
        {
            return false;
        }

        var level = LevelUtils.ResolveLevel(
            GuestGameSession.Game, spawn.LevelId, spawn.SubLevelIndex);
        if (level == null)
        {
            return false;
        }

        var levelKey = LevelKey.Make(spawn.LevelId, spawn.SubLevelIndex);
        var needsCleanup = !this.authoritativeLevels.Contains(levelKey);

        if (needsCleanup)
        {
            // A sub-level can become available after the initial world takeover.
            this.ClearLocalMobs([level]);
        }

        var descriptor =
            GameDataSerializer.Deserialize<LevelMob>(spawn.EntityData);

        if (descriptor == null)
        {
            return false;
        }

        var mob = level.attachMob(descriptor);
        if (mob == null)
        {
            return false;
        }

        try
        {
            mob.hasGravity = false;
            mob.gravity = 0;
            mob.collisionMode = new CollisionMode.None();
            mob.frict = 0;
            if (needsCleanup)
            {
                this.authoritativeLevels.Add(levelKey);
            }
            Logger.Verbose("Mob spawned on client: netId={NetId} level={LevelId}#{SubLevelIndex}",
                spawn.NetId, spawn.LevelId, spawn.SubLevelIndex);
            native = mob;
            return true;
        }
        catch (Exception initializationError)
        {
            try
            {
                this.Despawn(spawn.NetId, mob, EntityDespawnReason.LevelUnloaded);
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(initializationError, cleanupError);
            }
            throw;
        }
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
    public void Despawn(uint netId, object? native, EntityDespawnReason reason)
    {
        try
        {
            if (native is Mob mob)
            {
                try
                {
                    this.mobModifiers.Detach(mob);
                }
                finally
                {
                    if (!this.IsDeathSuppressed(mob))
                    {
                        if (reason == EntityDespawnReason.LevelUnloaded)
                        {
                            // The index has already been detached; keep death hooks suppressed during destroy.
                            this.suppressedDeaths.Add(mob);
                            try
                            {
                                if (!mob.destroyed)
                                {
                                    mob.destroy();
                                }
                            }
                            finally
                            {
                                this.suppressedDeaths.Remove(mob);
                            }
                        }
                        else
                        {
                            this.combat.ForceNativeDeath(mob);
                        }
                    }
                }
            }
        }
        finally
        {
            this.states.Remove(netId);
            if (this.store.GetRecord(netId) == null)
            {
                this.confirmedDead.Remove(netId);
            }
        }
    }

    /// <inheritdoc />
    public void ApplyFrame(WorldFrame frame, Func<uint, bool> tryRecreate)
    {
        if (frame.Entities == null)
        {
            return;
        }

        foreach (var state in frame.Entities)
        {
            if (state == null || state.Kind != EntityKind.Mob)
            {
                continue;
            }
            if (state.Life <= 0)
            {
                this.confirmedDead.Add(state.NetId);
            }
            else
            {
                this.confirmedDead.Remove(state.NetId);
            }
        }

        // 服务端帧已给出 Life<=0 但还没进 DespawnedEntities，补充死亡
        this.ForceFrameDeadMobs(frame);

        // 本地预测致死后服务端仍存活，使用 NetId 重建
        this.RepairDestroyedMobs(frame, tryRecreate);

        // 有动态状态但没有本地实体也没有待生成数据时请求重同步
        foreach (var dynamic in frame.Entities)
        {
            if (dynamic == null || dynamic.Kind != EntityKind.Mob)
            {
                continue;
            }

            if (this.store.GetRecord(dynamic.NetId) == null)
            {
                this.requestResync("mob spawn missing");
            }
        }
        this.LogFirstFrame(frame);
    }

    private void LogFirstFrame(WorldFrame frame)
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
        // Carry NetId with the record instead of querying it back through a receiver.
        foreach (var record in this.store.Records.Where(record => record.Kind == EntityKind.Mob).ToArray())
        {
            if (record.Native is Mob { destroyed: false } mob)
            {
                this.states.ApplyInterpolated(record.NetId, mob, nowMs);
            }
        }
    }

    /// <inheritdoc />
    public void OnEpochReset()
    {
        // Entity pipeline releases all natives before resetting kind-specific state.
        this.states.Reset();
        this.authoritativeLevels.Clear();
        this.confirmedDead.Clear();
        this.suppressedDeaths.Clear();
        this.loggedFirstMob = false;
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

            if (this.store.Get(dynamic.NetId) is not Mob { destroyed: false } mob)
            {
                continue;
            }

            this.combat.ForceNativeDeath(mob);
        }
    }

    /// <summary>
    /// 预测错误时重建对应 mob
    /// </summary>
    private void RepairDestroyedMobs(WorldFrame frame, Func<uint, bool> tryRecreate)
    {
        foreach (var dynamic in frame.Entities)
        {
            if (dynamic == null || dynamic.Kind != EntityKind.Mob || dynamic.Life <= 0)
            {
                continue;
            }
            var record = this.store.GetRecord(dynamic.NetId);
            if (record == null || record.Native is Entity { destroyed: false })
            {
                continue;
            }
            // Policy belongs here; native ownership and replacement stay in the entity pipeline.
            if (tryRecreate(dynamic.NetId) && this.store.Get(dynamic.NetId) is Mob mob)
            {
                this.states.ApplyImmediate(dynamic, mob);
            }
        }
    }

    /// <inheritdoc />
    public void OnEntityEvent(EntityEvent evt)
    {
        if (evt is not MobModifierEvent mobEvt)
        {
            return;
        }

        if (this.store.Get(mobEvt.NetId) is not Mob mob || mob.destroyed)
        {
            return;
        }

        this.mobModifiers.DispatchClientEvent(
            mob, mobEvt, this.session.SyncedTimeMs);
    }

    /// <summary>
    /// Removes local mobs during world takeover and when a later sub-level is first replicated.
    /// </summary>
    public void ClearLocalMobs(IEnumerable<Level> levels)
    {
        var toDestroy = new List<Mob>();
        foreach (var level in levels)
        {
            var entities = level.entities;
            if (entities == null)
            {
                continue;
            }
            for (var i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Mob mob
                    && !this.IsRemoteMob(mob)
                    && !mob.destroyed)
                {
                    toDestroy.Add(mob);
                }
            }
        }

        // Keep every selected mob suppressed until the entire cleanup pass has finished.
        foreach (var mob in toDestroy)
        {
            this.suppressedDeaths.Add(mob);
        }

        List<Exception>? errors = null;
        try
        {
            foreach (var mob in toDestroy)
            {
                try
                {
                    mob.destroy();
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add(ex);
                }
            }
        }
        finally
        {
            foreach (var mob in toDestroy)
            {
                this.suppressedDeaths.Remove(mob);
            }
        }
        if (errors != null)
        {
            throw new AggregateException("Local mob cleanup failed", errors);
        }
    }
}
