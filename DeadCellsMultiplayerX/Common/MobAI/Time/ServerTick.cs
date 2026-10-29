namespace DeadCellsMultiplayerX.Common.MobAI.Time
{
    /// <summary>
    /// 服务端逻辑 Tick。
    /// </summary>
    public readonly record struct ServerTick(long Value)
    {
        /// <summary>
        /// 从毫秒时间换算为 Tick。
        /// </summary>
        public static ServerTick FromMs(long timeMs, int tickRate)
            => new(timeMs * tickRate / 1000);

        /// <summary>
        /// 换算回毫秒时间。
        /// </summary>
        public long ToMs(int tickRate)
            => Value * 1000 / tickRate;

        /// <summary>
        /// Tick 偏移。
        /// </summary>
        public static ServerTick operator +(ServerTick tick, long delta)
            => new(tick.Value + delta);

        /// <summary>
        /// Tick 偏移。
        /// </summary>
        public static ServerTick operator -(ServerTick tick, long delta)
            => new(tick.Value - delta);
    }
}
