namespace DeadCellsMultiplayerX.Server.Replication.Entities;

using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 服务端通用实体条目
/// </summary>
internal sealed class ServerEntityEntry
{
    public uint NetId { get; set; }

    public EntityKind Kind { get; set; }

    /// <summary>
    /// 引擎原生实体
    /// </summary>
    public object Native { get; set; } = null!;

    /// <summary>
    /// 类型相关的关卡描述数据
    /// </summary>
    public object? Descriptor { get; set; }

    /// <summary>
    /// 待广播的生成数据
    /// </summary>
    public EntitySpawn Spawn { get; set; } = new();

    /// <summary>
    /// 静态数据是否已序列化
    /// </summary>
    public bool SpawnSerialized { get; set; }

    /// <summary>
    /// 生成数据是否已广播
    /// </summary>
    public bool SpawnBroadcast { get; set; }
}
