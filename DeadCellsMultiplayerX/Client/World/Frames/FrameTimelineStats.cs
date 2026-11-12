namespace DeadCellsMultiplayerX.Client.World.Frames
{
    /// <summary>
    /// 世界帧应用时间与一秒窗口帧率统计。
    /// </summary>
    internal sealed class FrameTimelineStats
    {
        private long rateWindowStartMs = -1;
        private int rateFrames;

        /// <summary>
        /// 最近一次应用世界帧的本地时间毫秒。
        /// </summary>
        public long LastFrameAppliedAtMs { get; private set; } = -1;

        /// <summary>
        /// 最近一秒实际应用的帧率。
        /// </summary>
        public double AppliedFramesPerSecond { get; private set; }

        public void NoteApplied(long nowMs)
        {
            LastFrameAppliedAtMs = nowMs;

            if (rateWindowStartMs < 0)
            {
                rateWindowStartMs = nowMs;
                rateFrames = 0;
            }

            rateFrames++;
            long elapsed = nowMs - rateWindowStartMs;
            if (elapsed < 1000)
                return;

            AppliedFramesPerSecond = rateFrames * 1000.0 / elapsed;
            rateWindowStartMs = nowMs;
            rateFrames = 0;
        }

        public void Reset()
        {
            LastFrameAppliedAtMs = -1;
            AppliedFramesPerSecond = 0;
            rateWindowStartMs = -1;
            rateFrames = 0;
        }
    }
}
