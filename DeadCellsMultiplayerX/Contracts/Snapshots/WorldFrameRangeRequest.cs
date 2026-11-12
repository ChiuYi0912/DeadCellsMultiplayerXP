namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using MessagePack;

/// <summary>
/// 客户端请求服务端补发广播帧
/// </summary>
[MessagePackObject]
public class WorldFrameRangeRequest
{
    [Key(0)] public long FromSequence { get; set; }
    [Key(1)] public long ToSequence { get; set; }
}
