namespace DeadCellsMultiplayerX.Server.World.Replication.Frames;

using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Server.World.Replication.Areas;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using DeadCellsMultiplayerX.Server.World.Actors;
using Serilog;

/// <summary>
/// 服务端世界帧构建器
/// 每个 tick 组装一帧完整世界状态
/// 写入历史环，交由会话发送
/// 同时为客户端补发请求提供历史帧和全量基线
/// </summary>
internal sealed class WorldFrameBuilder
{
    /// <summary>
    /// 发送间隔秒数，对应 30Hz
    /// </summary>
    private const double SEND_INTERVAL_SECONDS = 1.0 / 30.0;

    /// <summary>
    /// 广播历史帧上限
    /// </summary>
    private const int MAX_HISTORY = 128;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "WorldFrameBuilder");

    /// <summary>
    /// 世界帧收集器
    /// </summary>
    private readonly ClientFrameCollector collector;

    /// <summary>
    /// 通用实体状态发布器
    /// </summary>
    private readonly EntityFrameBuilder entityBuilder;

    /// <summary>
    /// 本帧待广播的英雄动作
    /// </summary>
    private readonly ServerActorRuntime ghosts;

    /// <summary>
    /// 服务端动态关卡区域
    /// </summary>
    private readonly ServerLevelAreaAffectRegistry areaAffects;

    /// <summary>
    /// 各类实体的事件来源
    /// </summary>
    private readonly List<IServerEntityEventSource> eventSources = [];

    /// <summary>
    /// 广播历史帧
    /// </summary>
    private readonly List<WorldFrame> history = [];

    /// <summary>
    /// 发送时间累加器
    /// </summary>
    private double accumulator;

    /// <summary>
    /// 创建世界帧构建器
    /// </summary>
    public WorldFrameBuilder(
        ClientFrameCollector collector,
        EntityFrameBuilder entityBuilder,
        ServerActorRuntime ghosts,
        ServerLevelAreaAffectRegistry areaAffects)
    {
        this.collector = collector;
        this.entityBuilder = entityBuilder;
        this.ghosts = ghosts;
        this.areaAffects = areaAffects;

        this.RegisterEventSource(ghosts);
    }

    /// <summary>
    /// 注册一个实体事件来源；后续实体类型各自注册
    /// </summary>
    public void RegisterEventSource(IServerEntityEventSource source)
    {
        if (source != null && !this.eventSources.Contains(source))
        {
            this.eventSources.Add(source);
        }
    }

    /// <summary>
    /// 当前世界 epoch
    /// </summary>
    public int Epoch { get; private set; }

    /// <summary>
    /// 当前广播序号
    /// </summary>
    public long Sequence { get; private set; } = -1;

    /// <summary>
    /// 已构建并提交的帧数
    /// </summary>
    public int BuiltFrames { get; private set; }

    /// <summary>
    /// 补发请求服务次数
    /// </summary>
    public int BundleServed { get; private set; }

    /// <summary>
    /// 重同步服务次数
    /// </summary>
    public int ResyncServed { get; private set; }

    /// <summary>
    /// 世界重置 递增 epoch 序号归零 清空历史与收集器旧世界状态
    /// </summary>
    public void BeginEpoch(int epoch)
    {
        this.Epoch = epoch;
        this.Sequence = -1;
        this.accumulator = 0;
        this.history.Clear();
        this.collector.BeginEpoch(this.Epoch);
        Logger.Information("World epoch begin: epoch={Epoch}", this.Epoch);
    }

    /// <summary>
    /// 按发送间隔构建并提交一帧
    /// </summary>
    public WorldFrame? Build(double dt, long serverTimeMs)
    {
        this.accumulator += dt;
        if (this.accumulator < SEND_INTERVAL_SECONDS)
        {
            return null;
        }

        this.accumulator -= SEND_INTERVAL_SECONDS;

        return this.BuildFrame(serverTimeMs);
    }

    /// <summary>
    /// 按序号范围获取历史帧
    /// </summary>
    public WorldFrameBundle GetFrames(WorldFrameRangeRequest request, long serverTimeMs)
    {
        if (request == null
            || request.ToSequence < request.FromSequence
            || this.history.Count == 0)
        {
            this.ResyncServed++;
            return new WorldFrameBundle { Resync = this.BuildResync(serverTimeMs) };
        }

        var first = this.history[0].Sequence;
        var last = this.history[^1].Sequence;

        if (request.FromSequence < first || request.ToSequence > last)
        {
            this.ResyncServed++;
            var available = this.history
                .Where(f => f.Sequence >= request.FromSequence && f.Sequence <= request.ToSequence)
                .ToList();
            return new WorldFrameBundle
            {
                Frames = available,
                Resync = this.BuildResync(serverTimeMs),
            };
        }

        this.BundleServed++;
        var start = (int)(request.FromSequence - first);
        var count = (int)(request.ToSequence - request.FromSequence + 1);
        return new WorldFrameBundle { Frames = this.history.GetRange(start, count) };
    }

    /// <summary>
    /// 构建全量世界基线
    /// </summary>
    public WorldResync BuildResync(long serverTimeMs)
    {
        var entities = this.entityBuilder.BuildResync(serverTimeMs);

        return new WorldResync
        {
            Epoch = this.Epoch,
            Sequence = this.Sequence,
            ServerTimeMs = serverTimeMs,
            Heroes = [.. this.collector.GetActiveHeroDynamics()],
            Spawns = this.collector.GetAllSpawns(),
            Entities = entities.Entities,
            EntitySpawns = entities.Spawned,
            AreaAffects = this.areaAffects.Collect(serverTimeMs),
        };
    }

    /// <summary>
    /// 获取最新广播帧
    /// </summary>
    public WorldFrame? GetLatest()
        => this.history.Count > 0 ? this.history[^1] : null;

    public void Clear()
    {
        this.history.Clear();
        this.accumulator = 0;
    }

    /// <summary>
    /// 从所有实体事件来源收集本帧事件
    /// </summary>
    private List<EntityEvent> CollectEntityEvents(List<(IServerEntityEventSource Source, int Count)> pending)
    {
        if (this.eventSources.Count == 0)
        {
            return [];
        }

        var result = new List<EntityEvent>();
        foreach (var source in this.eventSources)
        {
            var events = source.PeekEntityEvents();
            result.AddRange(events);
            pending.Add((source, events.Count));
        }

        return result;
    }

    /// <summary>
    /// 组装并提交一帧世界状态
    /// </summary>
    private WorldFrame BuildFrame(long serverTimeMs)
    {
        this.ghosts.SyncTalismans();

        var entities = this.entityBuilder.Collect(serverTimeMs);
        List<(IServerEntityEventSource Source, int Count)> pending = [];
        var frame = new WorldFrame
        {
            Epoch = this.Epoch,
            Sequence = this.Sequence + 1,
            ServerTimeMs = serverTimeMs,
            Heroes = [.. this.collector.GetActiveHeroDynamics()],
            Spawned = this.collector.GetSpawnChanges(),
            Despawned = this.collector.GetRemoved(),
            Entities = entities.Entities,
            SpawnedEntities = entities.Spawned,
            DespawnedEntities = entities.Despawned,
            HeroOutcomes = this.ghosts.PeekOutcomes(),
            AreaAffects = this.areaAffects.Collect(serverTimeMs),
            EntityEvents = this.CollectEntityEvents(pending),
        };

        // Native reads finish before any queue is consumed.
        this.entityBuilder.Commit(serverTimeMs, entities);
        this.collector.CommitChanges(frame.Spawned.Count, frame.Despawned.Count);
        this.ghosts.CommitOutcomes(frame.HeroOutcomes.Count);
        foreach (var (source, count) in pending)
        {
            source.CommitEntityEvents(count);
        }
        this.Sequence = frame.Sequence;
        this.BuiltFrames++;

        if (this.BuiltFrames == 1)
        {
            Logger.Information(
                "First world frame emitted: epoch={Epoch} seq={Sequence} heroes={Heroes}",
                this.Epoch, this.Sequence, frame.Heroes.Count);
        }

        this.history.Add(frame);
        if (this.history.Count > MAX_HISTORY)
        {
            this.history.RemoveRange(0, this.history.Count - MAX_HISTORY);
        }

        return frame;
    }
}
