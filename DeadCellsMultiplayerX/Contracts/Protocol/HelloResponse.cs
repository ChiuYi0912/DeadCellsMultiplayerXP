namespace DeadCellsMultiplayerX.Contracts.Protocol;

using MessagePack;

[MessagePackObject]
public sealed class HelloResponse
{
    [Key(0)] public bool Accepted { get; set; }

    [Key(1)] public int ServerMajor { get; set; }

    [Key(2)] public int ServerMinor { get; set; }

    /// <summary>
    /// 拒绝原因；Accepted == true 时为空。
    /// </summary>
    [Key(3)] public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 当前世界 epoch；-1 表示世界尚未开始。
    /// </summary>
    [Key(4)] public int Epoch { get; set; } = -1;

    /// <summary>
    /// 服务端能力位。
    /// </summary>
    [Key(5)] public uint Capabilities { get; set; }
}
