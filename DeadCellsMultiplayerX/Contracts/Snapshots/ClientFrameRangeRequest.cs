using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// 服务端请求客户端补发上传帧
    /// </summary>
    [MessagePackObject]
    public class ClientFrameRangeRequest
    {
        [Key(0)] public long FromSequence { get; set; }
        [Key(1)] public long ToSequence { get; set; }
    }
}
