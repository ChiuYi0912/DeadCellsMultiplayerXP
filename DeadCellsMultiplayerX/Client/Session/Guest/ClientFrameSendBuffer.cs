namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc.en;
using DeadCellsMultiplayerX.Contracts.Snapshots;

using DeadCellsMultiplayerX.Engine.Utilities;

/// <summary>
/// 客户端发送缓冲
/// 每帧采集本地世界状态
/// 供服务端 NACK 补发
/// </summary>
internal sealed class ClientFrameSendBuffer
{
    /// <summary>
    /// 缓冲最大存储值
    /// </summary>
    private const int MAX_HISTORY = 256;

    /// <summary>
    ///定期重发静态数据,避免服务端跳缺口后永久缺失
    /// </summary>
    private const int SPAWN_REFRESH_FRAMES = 60;

    /// <summary>
    /// 缓冲数组
    /// 泛型:<see cref="ClientWorldFrame"/>
    /// </summary>
    private readonly List<ClientWorldFrame> history = [];

    /// <summary>
    /// 发送历史中最早一帧的序号
    /// 历史为空时无意义
    /// </summary>
    private long firstSequence;

    /// <summary>
    /// 客户端本地帧号
    /// 每构建一帧自增
    /// </summary>
    private long frameId;

    /// <summary>
    /// 上一次随帧发送的静态数据
    /// 用于判断静态数据是否变化
    /// </summary>
    private HeroSpawn? lastSpawn;

    /// <summary>
    /// 距离上次携带静态数据的帧数
    /// 达到阈值后强制重发
    /// </summary>
    private int framesSinceSpawn;

    /// <summary>
    /// 当前帧采集到的静态数据
    /// </summary>
    private HeroSpawn? capturedSpawn;

    /// <summary>
    /// 当前帧采集到的动态数据
    /// </summary>
    private HeroDynamic? capturedDynamic;

    /// <summary>
    /// 当前帧采集时的同步时间(毫秒)
    /// </summary>
    private long capturedTimeMs;

    /// <summary>
    /// 是否存在尚未发送的采集结果
    /// </summary>
    private bool hasCapture;

    /// <summary>
    /// 当前回显的服务端世界 epoch
    /// </summary>
    public int Epoch { get; private set; }

    /// <summary>
    /// 最近一次发送的帧序号
    /// </summary>
    public long LastSequence { get; private set; } = -1;

    /// <summary>
    /// 每帧采集当前本地英雄状态,失败时下一帧会重试
    /// </summary>
    /// <param name="hero"></param>
    /// <param name="guid">客户guid</param>
    /// <param name="syncedTimeMs">同步时间</param>
    /// <returns></returns>
    public bool Capture(Hero hero, string guid, long syncedTimeMs)
    {
        if (hero == null || hero.destroyed || !hero.initDone || hero.spr == null)
        {
            return false;
        }

        var (spawn, dyn) = HeroUtils.Collect(hero, guid, syncedTimeMs);
        this.capturedSpawn = spawn;
        this.capturedDynamic = dyn;
        this.capturedTimeMs = syncedTimeMs;
        this.hasCapture = true;
        return true;
    }

    /// <summary>
    /// 用最近一次采集构建世界帧,分配序号并写入发送历史
    /// </summary>
    public ClientWorldFrame? BuildFrameAndRecord()
    {
        if (!this.hasCapture || this.capturedDynamic == null)
        {
            return null;
        }

        var sequence = this.LastSequence + 1;
        var frame = ++this.frameId;

        var upload = new HeroUpload
        {
            ClientTime = this.capturedTimeMs,
            FrameId = (uint)frame,
            Dynamic = this.capturedDynamic,
        };

        // 静态数据在首次、变化时附带,并定期重发作为关键帧
        this.framesSinceSpawn++;
        if (this.capturedSpawn != null
            && (this.lastSpawn == null
                || !this.capturedSpawn.SameAs(this.lastSpawn)
                || this.framesSinceSpawn >= SPAWN_REFRESH_FRAMES))
        {
            upload.Spawn = this.capturedSpawn;
            this.lastSpawn = this.capturedSpawn;
            this.framesSinceSpawn = 0;
        }

        var result = new ClientWorldFrame
        {
            Epoch = this.Epoch,
            Sequence = sequence,
            ClientTimeMs = this.capturedTimeMs,
            FrameId = frame,
            Heroes = [upload],
        };

        this.history.Add(result);
        if (this.history.Count == 1)
        {
            this.firstSequence = sequence;
        }

        this.LastSequence = sequence;

        if (this.history.Count > MAX_HISTORY)
        {
            this.history.RemoveRange(0, this.history.Count - MAX_HISTORY);
            this.firstSequence = this.history[0].Sequence;
        }

        this.hasCapture = false;
        this.capturedSpawn = null;
        this.capturedDynamic = null;
        return result;
    }

    /// <summary>
    /// 按序号返回可补发的帧;历史已淘汰时标记 Unavailable
    /// </summary>
    /// <param name="request">帧数据</param>
    /// <returns><see cref="ClientFrameBundle"/></returns>
    public ClientFrameBundle BuildBundle(ClientFrameRangeRequest request)
    {
        var bundle = new ClientFrameBundle();
        if (request == null || request.ToSequence < request.FromSequence)
        {
            bundle.Unavailable = true;
            return bundle;
        }

        if (this.history.Count == 0
            || request.FromSequence < this.firstSequence
            || request.ToSequence > this.LastSequence)
        {
            bundle.Unavailable = true;
        }

        var start = Math.Max(request.FromSequence, this.firstSequence);
        var end = Math.Min(request.ToSequence, this.LastSequence);
        for (var seq = start; seq <= end; seq++)
        {
            var index = (int)(seq - this.firstSequence);
            if (index >= 0 && index < this.history.Count)
            {
                bundle.Frames.Add(this.history[index]);
            }
        }

        return bundle;
    }

    /// <summary>
    /// 观测到更大的服务端 epoch:清空历史并重置序号
    /// </summary>
    /// <param name="epoch"></param>
    public void SetEpoch(int epoch)
    {
        if (epoch <= this.Epoch)
        {
            return;
        }

        this.Epoch = epoch;
        this.history.Clear();
        this.firstSequence = 0;
        this.LastSequence = -1;
        this.frameId = 0;
        this.lastSpawn = null;
        this.framesSinceSpawn = 0;
        this.ClearCapture();
    }

    /// <summary>
    /// 本地英雄重新初始化
    /// </summary>
    /// <remarks>用于下一帧强制携带静态数据</remarks>
    public void ResetLocalHeroState()
    {
        this.lastSpawn = null;
        this.framesSinceSpawn = 0;
        this.ClearCapture();
    }

    public void Clear()
    {
        this.history.Clear();
        this.firstSequence = 0;
        this.LastSequence = -1;
        this.frameId = 0;
        this.lastSpawn = null;
        this.framesSinceSpawn = 0;
        this.ClearCapture();
    }

    private void ClearCapture()
    {
        this.hasCapture = false;
        this.capturedSpawn = null;
        this.capturedDynamic = null;
    }
}
