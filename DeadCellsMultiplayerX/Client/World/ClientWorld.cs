namespace DeadCellsMultiplayerX.Client.World;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Client.World.Mobs.Replication;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>
/// 客户端世界组合根
/// </summary>
internal sealed class ClientWorld : Disposable
{
    /// <summary>
    /// 接收、补帧与重同步；缓存数据后交付有序世界帧
    /// </summary>
    private readonly WorldFrameReceiver frameReceiver;

    private readonly Action<WorldFrame> acknowledgeCommands;

    /// <summary>
    /// 远程英雄管理器，负责维护其他玩家的显示实体
    /// </summary>
    private readonly RemoteHeroManager remoteHeroes;

    /// <summary>
    /// 通用实体存储
    /// </summary>
    private readonly ClientEntityStore entityStore;

    /// <summary>
    /// 通用实体帧管线
    /// </summary>
    private readonly ClientEntityReplication entityReplication;

    /// <summary>
    /// Mob 同步装置，负责服务端怪物的生成、状态同步与销毁
    /// </summary>
    private readonly ClientMobReplicator remoteMobs;

    /// <summary>
    /// 本地英雄状态
    /// </summary>
    private readonly LocalHeroTracker localHeroTracker;

    /// <summary>
    /// 远端动作结果的去重与延迟派发；实体及代理归 RemoteHero 所有
    /// </summary>
    private readonly HeroActionReplay actionReplay;

    /// <summary>
    /// 服务端权威的动态关卡区域镜像
    /// </summary>
    private readonly ClientAreaAffectState areaAffects;
    private bool started;

    public ClientWorld(GuestGameSession session, string localGuid, int minimumEpochExclusive = -1)
    {
        try
        {
            this.acknowledgeCommands = session.AcknowledgeCommands;
            this.frameReceiver = new WorldFrameReceiver(session, minimumEpochExclusive);
            this.entityStore = new ClientEntityStore();

            this.localHeroTracker = new LocalHeroTracker(session.CanRepairHeroTargetability);
            this.remoteHeroes = new RemoteHeroManager(session, this.localHeroTracker.State, localGuid, this.InterruptRemoteMobSkill,
                this.frameReceiver.History, this.frameReceiver.HeroSpawns);
            this.actionReplay = new HeroActionReplay(localGuid, this.remoteHeroes.Present);
            this.remoteHeroes.HeroRemoved += this.actionReplay.RemoveHero;
            this.MobModifiers = new ClientMobModifierRegistry(
                session, this.entityStore, this.remoteHeroes);
            this.remoteMobs = new ClientMobReplicator(
                session, this.MobModifiers, this.frameReceiver.History, this.entityStore, this.frameReceiver.RequestResync);
            this.entityReplication = new ClientEntityReplication(this.entityStore);
            this.entityReplication.RegisterHandler(this.remoteMobs);
            this.entityReplication.DebugLabels.Register(new MobDebugTextProvider(session));

            this.areaAffects = new ClientAreaAffectState(session);

            this.frameReceiver.FrameDelivered += this.ApplyDeliveredFrame;
            this.frameReceiver.BaselineDelivered += this.OnBaselineDelivered;
        }
        catch
        {
            // Native hero/level initialization can fail after some children
            // have registered events. Roll back while this owner is reachable.
            this.Dispose();
            throw;
        }
    }

    /// <summary>Raised after a baseline has been applied to this world's components.</summary>
    public event Action<ClientWorld, int>? BaselineApplied;

    /// <summary>The owner must publish this world before starting network delivery.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        if (this.started)
        {
            return;
        }
        this.started = true;
        this.frameReceiver.Start();
    }

    /// <summary>
    /// 处理接收到的世界帧
    /// </summary>
    public void OnWorldFrame(WorldFrame frame)
    {
        if (this.started && !this.IsDisposed)
        {
            this.frameReceiver.Receive(frame);
        }
    }

    /// <summary>
    /// 推进世界帧与怪物同步的定时逻辑
    /// </summary>
    public void Tick(long nowMs)
    {
        if (!this.started || this.IsDisposed)
        {
            return;
        }
        this.remoteHeroes.UpdateActionProxies();
        if (this.IsDisposed)
        {
            return;
        }
        this.actionReplay.Tick(nowMs);
        if (this.IsDisposed)
        {
            return;
        }
        this.frameReceiver.Tick(nowMs);
        if (this.IsDisposed)
        {
            return;
        }
        this.entityReplication.Tick(nowMs);
    }

    /// <summary>
    /// 当前世界帧 epoch
    /// </summary>
    internal int Epoch => this.frameReceiver.Epoch;

    /// <summary>
    /// 客户端 mob 修改器入口
    /// </summary>
    internal ClientMobModifierRegistry MobModifiers { get; }

    /// <summary>
    /// 当前是否正在应用服务端区域效果
    /// </summary>
    internal bool IsApplyingServerAreaAffect => this.areaAffects.IsApplyingServer;

    /// <summary>
    /// 判断实体是否为服务端同步的怪物
    /// </summary>
    internal bool IsRemoteMob(Mob mob) => this.remoteMobs.IsRemoteMob(mob);

    internal bool IsDeathSuppressed(Mob mob) => this.remoteMobs.IsDeathSuppressed(mob);

    /// <summary>
    /// 查询 NetId 对应的远端 mob。
    /// </summary>
    internal bool TryGetRemoteMob(uint netId, out Mob mob)
    {
        if (this.entityStore.Get(netId) is Mob typed && !typed.destroyed)
        {
            mob = typed;
            return true;
        }

        mob = null!;
        return false;
    }

    /// <summary>
    /// 采集调试 UI 所需的只读快照
    /// </summary>
    internal ClientWorldStats CaptureStats()
    {
        var mobCount = 0;
        var heroCount = this.remoteHeroes.Count;
        var entityCount = 0;
        var pendingSpawns = 0;
        foreach (var record in this.entityStore.Records)
        {
            entityCount++;
            if (record.Kind == EntityKind.Mob && record.Native is Mob { destroyed: false })
            {
                mobCount++;
            }
            if (!record.Created)
            {
                pendingSpawns++;
            }
        }

        var local = this.localHeroTracker.State;
        return new ClientWorldStats(
            this.frameReceiver.Epoch,
            this.frameReceiver.LastFrameAppliedAtMs,
            this.frameReceiver.AppliedFramesPerSecond,
            this.frameReceiver.HasBaseline,
            this.frameReceiver.LastDeliveredSequence,
            this.frameReceiver.PendingCount,
            this.frameReceiver.BufferedFrames,
            this.frameReceiver.GapCount,
            this.frameReceiver.NackSent,
            this.frameReceiver.BundleReceived,
            this.frameReceiver.ResyncCount,
            this.frameReceiver.SkippedFrames,
            this.frameReceiver.DroppedOld,
            this.frameReceiver.PendingOverflow,
            mobCount,
            this.remoteHeroes.ActionProxyCount,
            heroCount,
            entityCount,
            pendingSpawns,
            local.CurrentLevelId,
            local.SubLevelIndex,
            local.HasHero,
            local.IsAlive);
    }

    /// <summary>
    /// 在世界帧应用完成后，将帧数据应用到远程英雄与怪物
    /// </summary>
    private void ApplyDeliveredFrame(WorldFrame frame)
    {
        if (this.IsDisposed)
        {
            return;
        }
        // 确认不等待视觉重放；补发帧同样经过时间线后到达这里。
        this.acknowledgeCommands(frame);
        if (this.IsDisposed)
        {
            return;
        }
        this.actionReplay.BeginFrame(frame);
        this.remoteHeroes.ApplyFrame(frame);
        if (this.IsDisposed)
        {
            return;
        }
        this.areaAffects.ApplyFrame(frame);
        if (this.IsDisposed)
        {
            return;
        }
        this.actionReplay.ApplyFrame(frame);
        if (this.IsDisposed)
        {
            return;
        }
        this.entityReplication.ApplyFrame(frame);
    }

    private void OnBaselineDelivered(int epoch)
    {
        if (this.started && !this.IsDisposed && this.Epoch == epoch)
        {
            this.BaselineApplied?.Invoke(this, epoch);
        }
    }

    protected override void MyDispose()
    {
        this.BaselineApplied = null;
        this.frameReceiver?.FrameDelivered -= this.ApplyDeliveredFrame;
        this.frameReceiver?.BaselineDelivered -= this.OnBaselineDelivered;
        this.DisposeSafely(this.frameReceiver, "world frame receiver");
        if (this.remoteHeroes != null && this.actionReplay != null)
        {
            this.remoteHeroes.HeroRemoved -= this.actionReplay.RemoveHero;
        }
        this.DisposeSafely(this.actionReplay, "action replay");
        try
        {
            this.MobModifiers?.Clear();
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Mob modifier cleanup failed");
        }
        this.DisposeSafely(this.entityReplication, "entity replication");
        this.DisposeSafely(this.areaAffects, "area affects");
        this.DisposeSafely(this.remoteHeroes, "remote heroes");
        this.DisposeSafely(this.localHeroTracker, "local hero tracker");
    }

    private void InterruptRemoteMobSkill(uint netId)
    {
        if (this.IsDisposed || netId == 0 || !this.TryGetRemoteMob(netId, out var mob))
        {
            return;
        }

        var animation = mob.spr?.get_anim();
        if (animation != null && !animation.destroyed)
        {
            animation.stopWithStateAnims();
        }
    }

}
