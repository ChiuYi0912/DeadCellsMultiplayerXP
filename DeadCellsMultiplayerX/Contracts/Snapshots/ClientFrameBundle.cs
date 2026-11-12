namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using MessagePack;

/// <summary>
/// 客户端对补发请求的响应
/// </summary>
[MessagePackObject]
public class ClientFrameBundle
{
    [Key(0)] public List<ClientWorldFrame> Frames { get; set; } = [];
    [Key(1)] public bool Unavailable { get; set; }
}
