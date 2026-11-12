using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// 服务端对补发请求的响应
    /// </summary>
    [MessagePackObject]
    public class WorldFrameBundle
    {
        [Key(0)] public List<WorldFrame> Frames { get; set; } = [];
        [Key(1)] public WorldResync? Resync { get; set; }
    }
}
