namespace DeadCellsMultiplayerX.Client.World
{
    /// <summary>
    /// 客户端世界的只读诊断快照；供调试 UI 使用，避免直接访问各子系统。
    /// </summary>
    internal readonly record struct ClientWorldStats(
        int Epoch,
        long LastFrameAppliedAtMs,
        double AppliedFramesPerSecond,
        bool HasBaseline,
        long LastDeliveredSequence,
        int PendingCount,
        int BufferedFrames,
        int GapCount,
        int NackSent,
        int BundleReceived,
        int ResyncCount,
        int SkippedFrames,
        int DroppedOld,
        int PendingOverflow,
        int Mobs,
        int Ghosts,
        int Heroes,
        int Entities,
        int PendingSpawns,
        string LevelId,
        int SubLevelIndex,
        bool HasHero,
        bool IsAlive);
}
