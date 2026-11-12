namespace DeadCellsMultiplayerX.Client.World.Frames;

using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Frames;
using Serilog;

/// <summary>
/// 客户端世界帧接收管理器。
/// 负责 epoch 校验、乱序重排、缺口补发与重同步；
/// 已应用帧的插值历史、静态数据索引与帧统计由独立协作者维护。
/// </summary>
internal sealed class WorldFrameTimeline
{
    /// <summary>
    /// 乱序待处理帧上限。
    /// </summary>
    private const int MAX_PENDING = 64;

    /// <summary>
    /// 单次补发请求的最大帧数。
    /// </summary>
    private const int MAX_NACK_RANGE = 64;

    /// <summary>
    /// 首次补发请求延迟毫秒。
    /// </summary>
    private const long NACK_FIRST_DELAY_MS = 50;

    /// <summary>
    /// 补发请求重试间隔毫秒。
    /// </summary>
    private const long NACK_RETRY_MS = 100;

    /// <summary>
    /// 缺口跳过超时毫秒。
    /// </summary>
    private const long GAP_SKIP_MS = 250;

    /// <summary>
    /// 缺口跳过帧数阈值。
    /// </summary>
    private const int GAP_SKIP_COUNT = 16;

    /// <summary>
    /// 重同步请求重试间隔毫秒。
    /// </summary>
    private const long RESYNC_RETRY_MS = 1000;

    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "WorldFrameTimeline");

    private readonly GuestClientSession session;
    private readonly WorldFrameHistory history = new();
    private readonly FrameSpawnIndex spawns = new();
    private readonly FrameTimelineStats stats = new();

    /// <summary>
    /// 序列/缺口/NACK 状态机；客户端接收策略在构造函数中显式给出。
    /// </summary>
    private readonly FrameSequencer<WorldFrame> sequencer;

    /// <summary>
    /// 上次重同步请求本地时间毫秒。
    /// </summary>
    private long lastResyncRequestLocalMs;

    /// <summary>
    /// 当前重同步请求任务。
    /// </summary>
    private Task<WorldResync>? resyncTask;

    /// <summary>
    /// 是否已记录首帧日志。
    /// </summary>
    private bool loggedFirstFrame;

    public WorldFrameTimeline(GuestClientSession session, string localGuid)
    {
        this.session = session;

        this.sequencer = new FrameSequencer<WorldFrame>(
            new FrameSequencerPolicy
            {
                MaxPending = MAX_PENDING,
                MaxNackRange = MAX_NACK_RANGE,
                NackFirstDelayMs = NACK_FIRST_DELAY_MS,
                NackRetryMs = NACK_RETRY_MS,
                GapSkipMs = GAP_SKIP_MS,
                GapSkipCount = GAP_SKIP_COUNT,
                ResyncOnLargeGap = true,
                ResyncGapMs = 1000,
                ResyncGapFrameCount = MAX_NACK_RANGE,
                OverflowMode = FrameOverflowMode.RequestResync,
                GapSkipMode = FrameGapSkipMode.Earliest,
                SkipOnUnavailable = false,
            },
            frame => frame.Sequence,
            this.ApplyFrame,
            hasBaseline: false,
            requestFrames: this.RequestFramesAsync,
            requestResync: this.RequestResync,
            applyResync: payload =>
            {
                if (payload is WorldResync resync)
                {
                    this.OnResync(resync);
                }
            },
            log: message => Logger.Warning("{Message}", message));

#if DEBUG
        WorldFrameDebug.ClientManager = this;
#endif
    }

    /// <summary>
    /// 当前服务端世界 epoch
    /// </summary>
    public int Epoch { get; private set; } = -1;

    /// <summary>
    /// 最近已交付的帧序号
    /// </summary>
    public long LastDeliveredSequence => this.sequencer.LastDelivered;

    /// <summary>
    /// 缺口次数
    /// </summary>
    public int GapCount => this.sequencer.GapCount;

    /// <summary>
    /// 补发请求次数
    /// </summary>
    public int NackSent => this.sequencer.NackSent;

    /// <summary>
    /// 补发响应接收次数
    /// </summary>
    public int BundleReceived => this.sequencer.BundleReceived;

    /// <summary>
    /// 跳过的帧数
    /// </summary>
    public int SkippedFrames => this.sequencer.SkippedFrames;

    /// <summary>
    /// 重同步请求次数
    /// </summary>
    public int ResyncCount { get; private set; }

    /// <summary>
    /// 丢弃的过期帧数
    /// </summary>
    public int DroppedOld => this.sequencer.DroppedOld;

    /// <summary>
    /// 待处理帧溢出次数
    /// </summary>
    public int PendingOverflow => this.sequencer.PendingOverflow;

    /// <summary>
    /// 待处理帧数量
    /// </summary>
    public int PendingCount => this.sequencer.PendingCount;

    /// <summary>
    /// 是否已建立渲染基线
    /// </summary>
    public bool HasBaseline => this.sequencer.HasBaseline;

    /// <summary>
    /// 最近一次应用世界帧的本地时间毫秒
    /// </summary>
    public long LastFrameAppliedAtMs => this.stats.LastFrameAppliedAtMs;

    /// <summary>
    /// 最近一秒实际应用的帧率
    /// </summary>
    public double AppliedFramesPerSecond => this.stats.AppliedFramesPerSecond;

    /// <summary>
    /// 渲染历史中缓存的帧数
    /// </summary>
    public int BufferedFrames => this.history.Count;

    /// <summary>
    /// 连续帧写入时间线后触发
    /// </summary>
    public event Action<WorldFrame>? FrameApplied;

    public void Start() => this.sequencer.Start();

    /// <summary>
    /// 服务端广播入口
    /// </summary>
    public void OnFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        // 首次收到世界帧 先记录 epoch 并请求基线
        if (this.Epoch < 0)
        {
            this.Epoch = frame.Epoch;
            this.session.ObserveWorldEpoch(this.Epoch);
            this.RequestResync("initial");
        }
        // 服务端世界重载 清空旧状态并重新建立基线
        else if (frame.Epoch > this.Epoch)
        {
            this.Reset();
            this.Epoch = frame.Epoch;
            this.session.ObserveWorldEpoch(this.Epoch);
            this.RequestResync("epoch changed");
        }
        // 旧 epoch 的迟到帧直接丢弃
        else if (frame.Epoch < this.Epoch)
        {
            this.sequencer.NoteDroppedOld();
            return;
        }

        // 用最新服务端时间校正本地时钟
        this.session.ObserveServerTime((long)frame.ServerTimeMs);

        // 序列/去重/乱序由共享 sequencer 处理
        this.sequencer.Receive(frame);
    }

    /// <summary>
    /// 处理重同步 补发与缺口
    /// </summary>
    public void Tick(long nowMs)
    {
#if DEBUG
        // 调试命令强制重同步
        if (WorldFrameDebug.ConsumeForceResync())
        {
            this.resyncTask = null;
            this.lastResyncRequestLocalMs = 0;
            this.RequestResync("debug");
        }
#endif

        // 轮询异步请求结果 避免阻塞游戏线程
        this.PollResync();

        // 序列/缺口/NACK 状态机共享实现
        this.sequencer.Tick(nowMs);
    }

    /// <summary>
    /// 在世界帧中查找英雄前后状态
    /// </summary>
    public bool TryGetHeroBracket(
        string guid,
        double nowSec,
        out HeroDynamic previous,
        out HeroDynamic next,
        out double alpha,
        out double renderTimeSec)
        => this.history.TryGetHeroBracket(
            guid, nowSec, out previous, out next, out alpha, out renderTimeSec);

    /// <summary>
    /// 在世界帧中查找怪物前后状态
    /// </summary>
    public bool TryGetEntityBracket(
        uint netId,
        double nowSec,
        out EntityDynamic previous,
        out EntityDynamic next,
        out double alpha,
        out double renderTimeSec)
        => this.history.TryGetEntityBracket(
            netId, nowSec, out previous, out next, out alpha, out renderTimeSec);

    /// <summary>
    /// 尝试获取英雄静态数据
    /// </summary>
    public bool TryGetSpawn(string guid, out HeroSpawn spawn)
        => this.spawns.TryGet(guid, out spawn);

    /// <summary>
    /// 请求全量世界基线
    /// </summary>
    public void RequestResync(string reason)
    {
        // 已有请求在途 不重复发送
        if (this.resyncTask != null && !this.resyncTask.IsCompleted)
        {
            return;
        }

        // 限制重同步频率
        var now = Environment.TickCount64;
        if (now - this.lastResyncRequestLocalMs < RESYNC_RETRY_MS)
        {
            return;
        }

        this.lastResyncRequestLocalMs = now;
        this.ResyncCount++;

        // 正常同步原因记 Information 缺口或溢出记 Warning
        if (reason is "initial" or "epoch changed" or "baseline retry" or "debug")
        {
            Logger.Information(
                "Request world resync: epoch={Epoch} reason={Reason}", this.Epoch, reason);
        }
        else
        {
            Logger.Warning(
                "Request world resync: epoch={Epoch} reason={Reason}", this.Epoch, reason);
        }

        try
        {
            this.resyncTask = this.session.Server.RequestWorldResync(this.Epoch);
        }
        catch (Exception ex)
        {
            // 失败时清空任务 下一 tick 重试
            Logger.Warning(ex, "RequestWorldResync failed");
            this.resyncTask = null;
        }
    }

    /// <summary>
    /// 重置管理器状态
    /// </summary>
    public void Reset()
    {
        this.history.Clear();
        this.spawns.Clear();
        this.Epoch = -1;
        this.sequencer.Reset();
        this.lastResyncRequestLocalMs = 0;
        this.resyncTask = null;
        this.loggedFirstFrame = false;
        this.stats.Reset();
    }

    public void Dispose()
    {
        this.Reset();
        FrameApplied = null;

#if DEBUG
        if (WorldFrameDebug.ClientManager == this)
        {
            WorldFrameDebug.ClientManager = null;
        }
#endif
    }

    /// <summary>
    /// 应用全量世界基线
    /// </summary>
    public void OnResync(WorldResync resync)
    {
        if (resync == null)
        {
            return;
        }

        if (resync.Epoch < this.Epoch)
        {
            this.sequencer.NoteDroppedOld();
            return;
        }

        // 迟到的重同步响应不应把已前进的时间线回退
        if (this.sequencer.HasBaseline && resync.Epoch == this.Epoch
            && resync.Sequence < this.sequencer.LastDelivered)
        {
            this.sequencer.NoteDroppedOld();
            return;
        }

        // 更高 epoch 说明世界已重载 先整体重置
        if (resync.Epoch > this.Epoch)
        {
            this.Reset();
            this.Epoch = resync.Epoch;
        }

        this.session.ObserveWorldEpoch(resync.Epoch);
        this.session.ObserveServerTime((long)resync.ServerTimeMs);

        // 基线覆盖当前状态 清空旧时间线与缓存
        this.history.Clear();
        this.spawns.Clear();
        this.sequencer.AdoptBaseline(resync.Sequence);

        this.spawns.AddRange(resync.Spawns);

        // 将基线包装成普通世界帧 统一走 ApplyFrame
        var baseline = new WorldFrame
        {
            Epoch = resync.Epoch,
            Sequence = resync.Sequence,
            ServerTimeMs = resync.ServerTimeMs,
            Heroes = resync.Heroes ?? [],
            Spawned = resync.Spawns ?? [],
            Despawned = [],
            Entities = resync.Entities ?? [],
            SpawnedEntities = resync.EntitySpawns ?? [],
            DespawnedEntities = [],
            EntityEvents = [],
            AreaAffects = resync.AreaAffects ?? [],
        };

        this.sequencer.ApplyDirect(baseline);

        Logger.Information(
            "World resync applied: epoch={Epoch} seq={Sequence} heroes={Heroes}",
            resync.Epoch, resync.Sequence, baseline.Heroes.Count);
    }

    private async Task<FrameSequencerBatch<WorldFrame>> RequestFramesAsync(
        long from,
        long to)
    {
        var bundle = await this.session.Server.RequestWorldFrames(
            new WorldFrameRangeRequest
            {
                FromSequence = from,
                ToSequence = to,
            });

        if (bundle == null)
        {
            return FrameSequencerBatch<WorldFrame>.Empty;
        }

        // NACK frames still go through the epoch gate that OnFrame applies.
        var frames = (bundle.Frames ?? [])
            .Where(frame => frame != null && frame.Epoch == this.Epoch)
            .ToList();

        return new FrameSequencerBatch<WorldFrame>(
            frames,
            Unavailable: false,
            Resync: bundle.Resync);
    }

    private void PollResync()
    {
        if (this.resyncTask == null || !this.resyncTask.IsCompleted)
        {
            return;
        }

        if (this.resyncTask.IsCompletedSuccessfully)
        {
            var resync = this.resyncTask.Result;
            this.resyncTask = null;
            this.OnResync(resync);
        }
        else
        {
            Logger.Warning(this.resyncTask.Exception, "RequestWorldResync faulted");
            this.resyncTask = null;
        }
    }

    /// <summary>
    /// 应用连续帧
    /// </summary>
    private void ApplyFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        this.history.Add(frame, frame.ServerTimeMs / 1000.0);
        this.spawns.AddRange(frame.Spawned);

        // 首帧日志用于确认世界帧链路正常
        if (!this.loggedFirstFrame)
        {
            this.loggedFirstFrame = true;
            Logger.Information(
                "First world frame applied: epoch={Epoch} seq={Sequence} heroes={Heroes}",
                frame.Epoch, frame.Sequence, frame.Heroes?.Count ?? 0);
        }

        // 校验静态数据后通知实体做存在性对账
        this.ValidateSpawns(frame);
        FrameApplied?.Invoke(frame);

        this.stats.NoteApplied(Environment.TickCount64);
    }

    /// <summary>
    /// 校验英雄静态数据是否存在。
    /// 版本差异只记录，不触发全量 resync：服务端静态数据变化会通过普通
    /// Spawned 帧送达，若该帧丢失，客户端下一次周期性的 spawn 刷新会修复；
    /// 若每次版本差异都 resync，会把 20Hz 时间线反复清空，造成明显卡顿。
    /// </summary>
    private void ValidateSpawns(WorldFrame frame)
    {
        if (frame.Heroes == null)
        {
            return;
        }

        foreach (var hero in frame.Heroes)
        {
            if (hero == null)
            {
                continue;
            }

            if (!this.spawns.TryGet(hero.GUID, out var spawn))
            {
                this.RequestResync("spawn missing");
                return;
            }

            if (spawn.Version != hero.SpawnVersion)
            {
                Logger.Debug(
                    "Spawn version pending: guid={Guid} local={Local} remote={Remote}",
                    hero.GUID, spawn.Version, hero.SpawnVersion);
            }
        }
    }
}
