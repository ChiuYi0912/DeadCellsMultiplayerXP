namespace DeadCellsMultiplayerX.Common.MobAI.Time
{
    /// <summary>
    /// 客户端/服务端时间与 Tick 的纯转换工具。
    /// 不依赖游戏时间系统；具体时间源由调用方提供。
    /// </summary>
    public sealed class TickClock
    {
        /// <summary>
        /// 每秒 Tick 数。
        /// </summary>
        public int TickRate { get; }

        /// <summary>
        /// 服务端时间 - 本地时间的偏移（毫秒）。
        /// </summary>
        public long ServerTimeOffsetMs { get; set; }

        /// <summary>
        /// 创建 Tick 时钟。
        /// </summary>
        public TickClock(int tickRate, long serverTimeOffsetMs = 0)
        {
            TickRate = tickRate;
            ServerTimeOffsetMs = serverTimeOffsetMs;
        }

        /// <summary>
        /// 把本地毫秒时间换算为服务端 Tick。
        /// </summary>
        public ServerTick ToServerTick(long localTimeMs)
            => ServerTick.FromMs(localTimeMs + ServerTimeOffsetMs, TickRate);

        /// <summary>
        /// 把服务端 Tick 换算为本地毫秒时间。
        /// </summary>
        public long ToClientMs(ServerTick tick)
            => tick.ToMs(TickRate) - ServerTimeOffsetMs;
    }
}
