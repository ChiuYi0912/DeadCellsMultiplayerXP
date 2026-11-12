using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
namespace DeadCellsMultiplayerX.Server.Replication.Entities
{
    /// <summary>
    /// 服务端通用实体条目
    /// </summary>
    internal sealed class ServerEntityEntry
    {
        public uint NetId;

        public EntityKind Kind;

        /// <summary>
        /// 引擎原生实体
        /// </summary>
        public object Native = null!;

        /// <summary>
        /// 类型相关的关卡描述数据
        /// </summary>
        public object? Descriptor;

        /// <summary>
        /// 待广播的生成数据
        /// </summary>
        public EntitySpawn Spawn = new();

        /// <summary>
        /// 静态数据是否已序列化
        /// </summary>
        public bool SpawnSerialized;

        /// <summary>
        /// 生成数据是否已广播
        /// </summary>
        public bool SpawnBroadcast;
    }
}
