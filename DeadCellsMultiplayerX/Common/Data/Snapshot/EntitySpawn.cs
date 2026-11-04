using DeadCellsMultiplayerX.Common.Data.Entities;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 通用实体的静态生成数据
    /// EntityData 由各 EntityKind 自行解释
    /// </summary>
    [MessagePackObject]
    public class EntitySpawn
    {
        /// <summary>
        /// 网络实体 id
        /// </summary>
        [Key(0)] public uint NetId { get; set; }

        /// <summary>
        /// 实体类型
        /// </summary>
        [Key(1)] public EntityKind Kind { get; set; }

        /// <summary>
        /// 关卡 id
        /// </summary>
        [Key(2)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 子关卡索引
        /// </summary>
        [Key(3)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// 类型相关的静态数据
        /// </summary>
        [Key(4)] public byte[] EntityData { get; set; } = [];
    }
}
