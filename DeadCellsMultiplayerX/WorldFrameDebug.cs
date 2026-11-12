namespace DeadCellsMultiplayerX;

#if DEBUG
using DeadCellsMultiplayerX.Client.World.Frames;

/// <summary>
/// 世界帧调试开关:用于强制制造缺口,验证 NACK/重同步路径。
/// </summary>
internal static class WorldFrameDebug
{
    public static int DropServerFrames { get; set; }
    public static int SkipClientUploads { get; set; }
    public static bool ForceResync { get; set; }
    public static WorldFrameTimeline? ClientManager { get; set; }

    public static bool ConsumeDropServerFrame()
    {
        if (DropServerFrames <= 0)
        {
            return false;
        }

        DropServerFrames--;
        return true;
    }

    public static bool ConsumeSkipClientUpload()
    {
        if (SkipClientUploads <= 0)
        {
            return false;
        }

        SkipClientUploads--;
        return true;
    }

    public static bool ConsumeForceResync()
    {
        if (!ForceResync)
        {
            return false;
        }

        ForceResync = false;
        return true;
    }
}
#endif
