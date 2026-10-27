namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 客户端快照渲染与时间补偿共用的时间参数
    /// </summary>
    internal static class SnapshotTiming
    {
        /// <summary>
        /// 客户端渲染插值延迟秒数
        /// </summary>
        public const double InterpolationDelaySeconds = 0.1;

        /// <summary>
        /// 客户端渲染插值延迟毫秒数
        /// </summary>
        public const long InterpolationDelayMs = 100;
    }
}
