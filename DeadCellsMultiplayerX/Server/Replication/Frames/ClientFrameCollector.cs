namespace DeadCellsMultiplayerX.Server.Replication.Frames;

using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Frames;
using Serilog;

/// <summary>
/// 服务端世界帧收集器
/// 按 GUID 对客户端上传帧排序 去重 检测缺口
/// 并通过 NACK 请求补发
/// 每帧都是全量状态 因此缺口不影响最新状态的正确性
/// </summary>
internal sealed class ClientFrameCollector
{
    /// <summary>
    /// 单个客户端的接收通道
    /// </summary>
    private sealed class ClientChannel
    {
        /// <summary>
        /// 客户端 GUID
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// 最近已交付的完整帧
        /// </summary>
        public ClientWorldFrame? Latest { get; set; }

        /// <summary>
        /// 上次记录的静态数据
        /// </summary>
        public HeroSpawn? LastSpawn { get; set; }

        /// <summary>
        /// 静态数据版本
        /// </summary>
        public uint SpawnVersion { get; set; }

        /// <summary>
        /// 补发请求发送委托
        /// </summary>
        public Func<ClientFrameRangeRequest, Task<ClientFrameBundle>>? NackSender { get; set; }

        /// <summary>
        /// 每个客户端独立的序列/缺口/NACK 状态机。
        /// </summary>
        public FrameSequencer<ClientWorldFrame> Sequencer { get; set; } = null!;
    }

    /// <summary>
    /// 乱序待处理帧上限
    /// </summary>
    private const int MAX_PENDING = 32;

    /// <summary>
    /// 单次补发请求的最大帧数
    /// </summary>
    private const int MAX_NACK_RANGE = 64;

    /// <summary>
    /// 首次补发请求延迟毫秒
    /// </summary>
    private const long NACK_FIRST_DELAY_MS = 50;

    /// <summary>
    /// 补发请求重试间隔毫秒
    /// </summary>
    private const long NACK_RETRY_MS = 100;

    /// <summary>
    /// 缺口跳过超时毫秒
    /// </summary>
    private const long GAP_SKIP_MS = 250;

    /// <summary>
    /// 缺口跳过帧数阈值
    /// </summary>
    private const int GAP_SKIP_COUNT = 16;

    private static readonly ILogger Logger = Log.ForContext("SourceContext", "WorldFrameCollector");

    /// <summary>
    /// 按 GUID 索引的客户端通道
    /// </summary>
    private readonly Dictionary<string, ClientChannel> channels = [];

    /// <summary>
    /// 待广播的静态数据变化
    /// </summary>
    private readonly List<HeroSpawn> pendingSpawnChanges = [];

    /// <summary>
    /// 待广播的移除 GUID
    /// </summary>
    private readonly List<string> pendingRemoved = [];

    /// <summary>
    /// 当前世界 epoch
    /// </summary>
    public int Epoch { get; private set; }

    /// <summary>
    /// 缺口次数
    /// </summary>
    public int GapCount => this.channels.Values.Sum(c => c.Sequencer.GapCount);

    /// <summary>
    /// 补发请求次数
    /// </summary>
    public int NackSent => this.channels.Values.Sum(c => c.Sequencer.NackSent);

    /// <summary>
    /// 补发响应接收次数
    /// </summary>
    public int BundleReceived => this.channels.Values.Sum(c => c.Sequencer.BundleReceived);

    /// <summary>
    /// 跳过的帧数
    /// </summary>
    public int SkippedFrames => this.channels.Values.Sum(c => c.Sequencer.SkippedFrames);

    /// <summary>
    /// 丢弃的过期帧数
    /// </summary>
    public int DroppedOld => this.channels.Values.Sum(c => c.Sequencer.DroppedOld);

    /// <summary>
    /// 待处理帧溢出次数
    /// </summary>
    public int PendingOverflow => this.channels.Values.Sum(c => c.Sequencer.PendingOverflow);

    /// <summary>
    /// 注册客户端通道与补发委托
    /// </summary>
    public void Register(string guid, Func<ClientFrameRangeRequest, Task<ClientFrameBundle>> nackSender)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        this.GetOrCreate(guid).NackSender = nackSender;
    }

    /// <summary>
    /// 移除客户端通道并记录待广播移除
    /// </summary>
    public void Unregister(string guid)
    {
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        if (this.channels.Remove(guid))
        {
            this.pendingRemoved.Add(guid);
        }
    }

    /// <summary>
    /// 开始新的世界 epoch
    /// </summary>
    public void BeginEpoch(int epoch)
    {
        this.Epoch = epoch;
        this.pendingSpawnChanges.Clear();
        this.pendingRemoved.Clear();

        foreach (var channel in this.channels.Values)
        {
            channel.Sequencer.Reset();
            channel.Latest = null;
            channel.LastSpawn = null;
            channel.SpawnVersion = 0;
        }
    }

    /// <summary>
    /// 接收客户端上传帧
    /// </summary>
    public void OnFrame(string guid, ClientWorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        if (!this.channels.TryGetValue(guid, out var channel))
        {
            return;
        }

        if (frame.Epoch != this.Epoch)
        {
            channel.Sequencer.NoteDroppedOld();
            return;
        }

        if (!channel.Sequencer.HasBaseline)
        {
            channel.Sequencer.AdoptBaseline(frame.Sequence - 1);
        }

        channel.Sequencer.Receive(frame);
    }

    /// <summary>
    /// 处理补发响应与缺口
    /// </summary>
    public void Tick(long nowMs)
    {
        foreach (var channel in this.channels.Values)
        {
            channel.Sequencer.Tick(nowMs);
        }
    }

    /// <summary>
    /// 获取所有客户端最新的英雄动态状态
    /// </summary>
    /// <summary>
    /// 获取指定客户端最新的英雄状态
    /// </summary>
    public bool TryGetHeroState(string guid, out HeroDynamic state)
    {
        if (this.channels.TryGetValue(guid, out var channel) && channel.Latest?.Heroes != null)
        {
            foreach (var hero in channel.Latest.Heroes)
            {
                if (hero?.Dynamic != null)
                {
                    state = hero.Dynamic;
                    return true;
                }
            }
        }

        state = null!;
        return false;
    }

    /// <summary>
    /// 获取指定客户端最新的静态数据
    /// </summary>
    public bool TryGetHeroSpawn(string guid, out HeroSpawn spawn)
    {
        if (this.channels.TryGetValue(guid, out var channel)
            && channel.LastSpawn != null)
        {
            spawn = channel.LastSpawn;
            return true;
        }

        spawn = null!;
        return false;
    }

    public IReadOnlyCollection<HeroDynamic> GetActiveHeroDynamics()
    {
        var result = new List<HeroDynamic>();
        foreach (var channel in this.channels.Values)
        {
            // Keep Heroes and GetAllSpawns() consistent: a client whose
            // static spawn has not arrived yet must not appear as a hero
            // without HeroSpawn, otherwise the client requests a full
            // resync every second until the spawn catches up.
            if (channel.LastSpawn == null)
            {
                continue;
            }

            var frame = channel.Latest;
            if (frame?.Heroes == null)
            {
                continue;
            }

            foreach (var hero in frame.Heroes)
            {
                if (hero?.Dynamic != null)
                {
                    result.Add(CopyDynamic(hero.Dynamic));
                }
            }
        }
        return result;
    }

    /// <summary>
    /// 取出并清空静态数据变化
    /// </summary>
    public List<HeroSpawn> DrainSpawnChanges()
    {
        if (this.pendingSpawnChanges.Count == 0)
        {
            return [];
        }

        var result = this.pendingSpawnChanges.ToList();
        this.pendingSpawnChanges.Clear();
        return result;
    }

    /// <summary>
    /// 取出并清空待移除 GUID
    /// </summary>
    public List<string> DrainRemoved()
    {
        if (this.pendingRemoved.Count == 0)
        {
            return [];
        }

        var result = this.pendingRemoved.ToList();
        this.pendingRemoved.Clear();
        return result;
    }

    /// <summary>
    /// 获取所有客户端当前静态数据
    /// </summary>
    public List<HeroSpawn> GetAllSpawns()
    {
        var result = new List<HeroSpawn>();
        foreach (var channel in this.channels.Values)
        {
            if (channel.LastSpawn != null)
            {
                result.Add(channel.LastSpawn);
            }
        }

        return result;
    }

    /// <summary>
    /// 清空收集器
    /// </summary>
    public void Clear()
    {
        this.channels.Clear();
        this.pendingSpawnChanges.Clear();
        this.pendingRemoved.Clear();
    }

    /// <summary>
    /// 处理补发响应任务
    /// </summary>
    private void Accept(ClientChannel channel, ClientWorldFrame frame)
    {
        channel.Latest = frame;

        var upload = frame.Heroes?.FirstOrDefault(h => h?.Dynamic != null);
        if (upload == null)
        {
            return;
        }

        if (upload.Spawn != null)
        {
            if (channel.LastSpawn == null || !upload.Spawn.SameAs(channel.LastSpawn))
            {
                channel.SpawnVersion++;
                upload.Spawn.Version = channel.SpawnVersion;
                channel.LastSpawn = upload.Spawn;
                this.pendingSpawnChanges.Add(upload.Spawn);
            }
        }

        upload.Dynamic.SpawnVersion = channel.SpawnVersion;
    }

    private async Task<FrameSequencerBatch<ClientWorldFrame>> RequestFramesAsync(
        ClientChannel channel,
        long from,
        long to)
    {
        var sender = channel.NackSender;
        if (sender == null)
        {
            return FrameSequencerBatch<ClientWorldFrame>.Empty;
        }

        var bundle = await sender(new ClientFrameRangeRequest
        {
            FromSequence = from,
            ToSequence = to,
        });

        if (bundle == null)
        {
            return FrameSequencerBatch<ClientWorldFrame>.Empty;
        }

        // Mirrors the epoch gate that OnFrame applies to normal uploads.
        var frames = (bundle.Frames ?? [])
            .Where(frame => frame != null && frame.Epoch == this.Epoch)
            .ToList();

        return new FrameSequencerBatch<ClientWorldFrame>(
            frames,
            bundle.Unavailable);
    }

    /// <summary>
    /// 复制动态状态 避免历史帧与收集器共享可变字段
    /// </summary>
    private static HeroDynamic CopyDynamic(HeroDynamic source) => new HeroDynamic
    {
        GUID = source.GUID,
        ChangeMask = source.ChangeMask,
        Pos = source.Pos,
        Anim = source.Anim,
        Affects = source.Affects,
        Flags = source.Flags,
        LevelId = source.LevelId,
        SubLevelIndex = source.SubLevelIndex,
        SpawnVersion = source.SpawnVersion,
        Life = source.Life,
        MaxLife = source.MaxLife,
        IsDead = source.IsDead,
    };

    /// <summary>
    /// 获取或创建客户端通道
    /// </summary>
    private ClientChannel GetOrCreate(string guid)
    {
        if (this.channels.TryGetValue(guid, out var channel))
        {
            return channel;
        }

        channel = new ClientChannel { Guid = guid };
        channel.Sequencer = new FrameSequencer<ClientWorldFrame>(
            new FrameSequencerPolicy
            {
                MaxPending = MAX_PENDING,
                MaxNackRange = MAX_NACK_RANGE,
                NackFirstDelayMs = NACK_FIRST_DELAY_MS,
                NackRetryMs = NACK_RETRY_MS,
                GapSkipMs = GAP_SKIP_MS,
                GapSkipCount = GAP_SKIP_COUNT,
                ResyncOnLargeGap = false,
                OverflowMode = FrameOverflowMode.JumpToNewest,
                GapSkipMode = FrameGapSkipMode.Newest,
                SkipOnUnavailable = true,
            },
            frame => frame.Sequence,
            frame => this.Accept(channel, frame),
            hasBaseline: true,
            requestFrames: (from, to) => this.RequestFramesAsync(channel, from, to),
            log: message => Logger.Warning(
                "Client frame sequencer {Guid}: {Message}",
                channel.Guid, message));

        this.channels[guid] = channel;
        return channel;
    }
}
