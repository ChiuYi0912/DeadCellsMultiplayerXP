namespace DeadCellsMultiplayerX.Client.World.Frames;

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
        this.LastFrameAppliedAtMs = nowMs;

        if (this.rateWindowStartMs < 0)
        {
            this.rateWindowStartMs = nowMs;
            this.rateFrames = 0;
        }

        this.rateFrames++;
        var elapsed = nowMs - this.rateWindowStartMs;
        if (elapsed < 1000)
        {
            return;
        }

        this.AppliedFramesPerSecond = this.rateFrames * 1000.0 / elapsed;
        this.rateWindowStartMs = nowMs;
        this.rateFrames = 0;
    }

    public void Reset()
    {
        this.LastFrameAppliedAtMs = -1;
        this.AppliedFramesPerSecond = 0;
        this.rateWindowStartMs = -1;
        this.rateFrames = 0;
    }
}
