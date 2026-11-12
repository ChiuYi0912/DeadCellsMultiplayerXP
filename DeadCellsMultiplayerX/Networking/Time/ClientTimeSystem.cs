using System.Diagnostics;
namespace DeadCellsMultiplayerX.Networking.Time
{
    internal class ClientTimeSystem
    {
        //采样
        private const int SAMPLE_COUNT = 8;
        private readonly List<(long rtt, long offset)> samples = new();

        //基准
        private long offsetMs;          // serverTime - localTime
        private long driftCorrection;   // 慢速修正累积
        private bool ready;

        //修正参数
        private const long HARD_SYNC_THRESHOLD_MS = 100;
        private const long MAX_CORRECTION_PER_SNAPSHOT_MS = 2;

        //公开状态
        public bool Ready => ready;

        //最近一批采样中的最优 RTT
        public long RttMs { get; private set; } = -1;

        //本地时间相对服务端时间的偏移
        public long OffsetMs => offsetMs + driftCorrection;

        //当前估算的服务端时间
        public long SyncedTime => NowMs() + offsetMs + driftCorrection;

        //工具
        public static long NowMs() => Stopwatch.GetTimestamp() / TimeSpan.TicksPerMillisecond;

        //NTP 采样
        /// <summary>
        /// 记录一次 NTP 往返
        /// </summary>
        /// <param name="localT0">发送请求时的本地时间</param>
        /// <param name="localT1">收到响应时的本地时间</param>
        /// <param name="serverTime">服务端返回的时间戳</param>
        public void AddSample(long localT0, long localT1, long serverTime)
        {
            long rtt = localT1 - localT0;

            // 服务端处理时刻大致对应客户端时间的"中点"
            long localMid = (localT0 + localT1) / 2;

            // offset = serverTime - localMid
            long offset = serverTime - localMid;

            samples.Add((rtt, offset));

            if (samples.Count >= SAMPLE_COUNT)
            {
                // 取 RTT 最小的样本
                samples.Sort((a, b) => a.rtt.CompareTo(b.rtt));
                var best = samples[0];

                offsetMs = best.offset;
                driftCorrection = 0;
                ready = true;
                RttMs = best.rtt;

                samples.Clear();
            }
        }


        /// <summary>
        /// 快照驱动修正
        /// 收到快照时调用，用 ServerTime 校验并慢速修正
        /// </summary>
        public void OnSnapshot(long snapshotServerTime)
        {
            if (!ready) return;

            long predicted = SyncedTime;
            long drift = snapshotServerTime - predicted;

            if (Math.Abs(drift) > HARD_SYNC_THRESHOLD_MS)
            {
                // 漂移太大直接对齐
                offsetMs += drift;
                driftCorrection = 0;
            }
            else
            {
                // 漂移小,每次最多修 2ms
                driftCorrection += Math.Clamp(
                    drift,
                    -MAX_CORRECTION_PER_SNAPSHOT_MS,
                    MAX_CORRECTION_PER_SNAPSHOT_MS);
            }
        }
    }
}
