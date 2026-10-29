using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端权威怪物的生成数据
    /// </summary>
    [MessagePackObject]
    public class MobSpawn
    {
        /// <summary>
        /// 网络实体 id
        /// </summary>
        [Key(0)] public uint NetId { get; set; }

        /// <summary>
        /// 关卡 id
        /// </summary>
        [Key(1)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 子关卡索引
        /// </summary>
        [Key(2)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// MessagePackSerializerImpl 序列化的 dc.level.Mob
        /// </summary>
        [Key(3)] public byte[] EntityData { get; set; } = [];
    }
}
