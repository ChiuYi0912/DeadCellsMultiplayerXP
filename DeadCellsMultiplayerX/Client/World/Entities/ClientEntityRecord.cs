using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
namespace DeadCellsMultiplayerX.Client.World.Entities
{
    /// <summary>
    /// 客户端通用实体记录
    /// Native 为 null 表示生成数据已到但实体尚未创建
    /// </summary>
    internal sealed class ClientEntityRecord
    {
        public required EntitySpawn Spawn { get; init; }

        public object? Native;

        public uint NetId => Spawn.NetId;

        public EntityKind Kind => Spawn.Kind;

        public bool Created => Native != null;

        public LevelKey LevelKey => new(Spawn.LevelId, Spawn.SubLevelIndex);
    }
}
