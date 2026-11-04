using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Client.Replication.Entities
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

        public string LevelKey => LevelKeys.Make(Spawn.LevelId, Spawn.SubLevelIndex);
    }
}
