namespace DeadCellsMultiplayerX.Client.World.Entities;

using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 客户端通用实体记录
/// Native 为 null 表示生成数据已到但实体尚未创建
/// </summary>
internal sealed class ClientEntityRecord
{
    public required EntitySpawn Spawn { get; init; }

    public object? Native { get; set; }

    public uint NetId => this.Spawn.NetId;

    public EntityKind Kind => this.Spawn.Kind;

    public bool Created => this.Native != null;

    public LevelKey LevelKey => new(this.Spawn.LevelId, this.Spawn.SubLevelIndex);
}
