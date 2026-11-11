using MessagePack;

namespace DeadCellsMultiplayerX.Common.Contracts.Protocol
{
    [MessagePackObject]
    public sealed class HelloRequest
    {
        [Key(0)] public int Major { get; set; }

        [Key(1)] public int Minor { get; set; }

        /// <summary>
        /// 模组版本，仅用于诊断/大厅展示，不承担协议兼容职责。
        /// </summary>
        [Key(2)] public string ModVersion { get; set; } = string.Empty;

        /// <summary>
        /// 客户端能力位。
        /// </summary>
        [Key(3)] public uint Capabilities { get; set; }
    }
}
