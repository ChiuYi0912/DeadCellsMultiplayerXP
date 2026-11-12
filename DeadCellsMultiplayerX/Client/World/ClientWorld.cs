namespace DeadCellsMultiplayerX.Client.World;

using dc.en;
using DeadCellsMultiplayerX.Client.Presentation.Effects;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Client.World.Mobs.Replication;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>
/// 客户端世界组合根。
/// 负责创建并拥有子系统，对外只暴露帧路由、Tick 与少量只读查询；
/// 子系统之间通过构造参数获得各自的窄依赖，不再接收整个世界。
/// </summary>
internal sealed class ClientWorld : DisposableEventReceiver
{
    /// <summary>
    /// 世界帧管理器，负责接收、应用和驱动世界帧
    /// </summary>
    private readonly WorldFrameTimeline frames;

    /// <summary>
    /// 远程英雄管理器，负责维护其他玩家的显示实体
    /// </summary>
    private readonly RemoteHeroManager heroes;

    /// <summary>
    /// 通用实体存储
    /// </summary>
    private readonly ClientEntityStore entityStore;

    /// <summary>
    /// 通用实体帧管线
    /// </summary>
    private readonly ClientEntityFrameApplier entities;

    /// <summary>
    /// Mob 同步装置，负责服务端怪物的生成、状态同步与销毁
    /// </summary>
    private readonly ClientMobReplicator mobs;

    /// <summary>
    /// 本地英雄状态
    /// </summary>
    private readonly LocalHeroState localHero;

    /// <summary>
    /// 远端玩家 ClientHeroReplayProxy 生命周期与动作重放
    /// </summary>
    private readonly ClientReplayProxyManager ghosts;

    /// <summary>
    /// 服务端权威的动态关卡区域镜像
    /// </summary>
    private readonly ClientAreaAffectState areaAffects;

    public ClientWorld(GuestClientSession session, string localGuid)
    {
        this.frames = new WorldFrameTimeline(session, localGuid);
        this.entityStore = new ClientEntityStore();

        // Ghost 代理与可见 Hero 互相引用；先创建代理，再创建 Hero 后绑定查询。
        this.ghosts = new ClientReplayProxyManager(session, localGuid);
        this.MobModifiers = new ClientMobModifierRegistry(
            session, this.entityStore, this.ghosts);
        this.mobs = new ClientMobReplicator(
            session, this.MobModifiers, this.frames, this.entityStore);
        this.entityStore.RegisterHandler(this.mobs);

        this.entities = new ClientEntityFrameApplier(this.entityStore);
        this.entities.DebugLabels.Register(new MobDebugTextProvider(session));

        this.localHero = new LocalHeroState(session);
        this.heroes = new RemoteHeroManager(
            session, this.localHero, localGuid, this.ghosts);
        this.ghosts.BindRemoteHeroes(this.heroes);

        this.areaAffects = new ClientAreaAffectState(session);

        this.frames.FrameApplied += this.OnFrameApplied;
        this.frames.Start();
    }

    /// <summary>
    /// 处理接收到的世界帧
    /// </summary>
    public void OnWorldFrame(WorldFrame frame) => this.frames.OnFrame(frame);

    /// <summary>
    /// 推进世界帧与怪物同步的定时逻辑
    /// </summary>
    public void Tick(long nowMs)
    {
        this.ghosts.Tick(nowMs);
        this.frames.Tick(nowMs);
        this.entities.Tick(nowMs);
    }

    /// <summary>
    /// 当前世界帧 epoch
    /// </summary>
    internal int Epoch => this.frames.Epoch;

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
    internal bool IsRemoteMob(Mob mob) => this.mobs.IsRemoteMob(mob);

    internal bool IsDeathSuppressed(Mob mob) => this.mobs.IsDeathSuppressed(mob);

    /// <summary>
    /// 查询原生 mob 对应的 NetId
    /// </summary>
    internal bool TryGetMobNetId(Mob mob, out uint netId)
        => this.mobs.Receiver.TryGetNetId(mob, out netId);

    /// <summary>
    /// 采集调试 UI 所需的只读快照
    /// </summary>
    internal ClientWorldStats CaptureStats()
    {
        var mobCount = 0;
        foreach (var mob in this.mobs.Receiver.All)
        {
            if (!mob.destroyed)
            {
                mobCount++;
            }
        }

        var heroCount = 0;
        foreach (var _ in this.heroes.All)
        {
            heroCount++;
        }

        var entityCount = 0;
        var pendingSpawns = 0;
        foreach (var record in this.entityStore.Records)
        {
            entityCount++;
            if (!record.Created)
            {
                pendingSpawns++;
            }
        }

        var local = this.localHero.Context;
        return new ClientWorldStats(
            this.frames.Epoch,
            this.frames.LastFrameAppliedAtMs,
            this.frames.AppliedFramesPerSecond,
            this.frames.HasBaseline,
            this.frames.LastDeliveredSequence,
            this.frames.PendingCount,
            this.frames.BufferedFrames,
            this.frames.GapCount,
            this.frames.NackSent,
            this.frames.BundleReceived,
            this.frames.ResyncCount,
            this.frames.SkippedFrames,
            this.frames.DroppedOld,
            this.frames.PendingOverflow,
            mobCount,
            this.ghosts.Count,
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
    private void OnFrameApplied(WorldFrame frame)
    {
        this.ghosts.BeginFrame(frame);
        this.heroes.ApplyFrame(frame, this.frames);
        this.areaAffects.ApplyFrame(frame);
        this.ghosts.ApplyFrame(frame);
        this.entities.ApplyFrame(frame);
    }

    protected override void MyDispose()
    {
        this.frames.FrameApplied -= this.OnFrameApplied;
        this.MobModifiers.Clear();
        this.entities.Clear();
        this.areaAffects.Reset();
        this.heroes.Dispose();
        this.ghosts.Dispose();
        this.frames.Dispose();
        this.localHero.Dispose();
        base.MyDispose();
    }
}
