using DeadCellsMultiplayerX.Contracts.Entities;
using MessagePack;


namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// 通用实体的每帧动态状态
    /// </summary>
    [MessagePackObject]
    public class EntityDynamic
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
        /// 位置与速度
        /// </summary>
        [Key(2)] public PosVector Pos { get; set; } = new();

        /// <summary>
        /// 动画状态
        /// </summary>
        [Key(3)] public AnimInfo Anim { get; set; } = new();

        /// <summary>
        /// 当前生命
        /// </summary>
        [Key(4)] public int Life { get; set; }

        /// <summary>
        /// 最大生命
        /// </summary>
        [Key(5)] public int MaxLife { get; set; }

        /// <summary>
        /// 关卡 id
        /// </summary>
        [Key(6)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 子关卡索引
        /// </summary>
        [Key(7)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// 影响状态
        /// </summary>
        [Key(8)] public List<AffectEntry> Affects { get; set; } = [];

        /// <summary>
        /// 类型相关的附加状态
        /// </summary>
        [Key(9)] public byte[]? Extra { get; set; }
    }
}
