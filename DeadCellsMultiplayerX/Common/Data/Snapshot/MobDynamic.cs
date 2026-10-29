using System.Collections.Generic;
using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端权威怪物的动态状态
    /// </summary>
    [MessagePackObject]
    public class MobDynamic
    {
        /// <summary>
        /// 网络实体 id
        /// </summary>
        [Key(0)] public uint NetId { get; set; }

        /// <summary>
        /// 位置状态
        /// </summary>
        [Key(1)] public PosVector Pos { get; set; } = new();

        /// <summary>
        /// 动画状态
        /// </summary>
        [Key(2)] public AnimInfo Anim { get; set; } = new();

        /// <summary>
        /// 当前生命
        /// </summary>
        [Key(3)] public int Life { get; set; }

        /// <summary>
        /// 最大生命
        /// </summary>
        [Key(4)] public int MaxLife { get; set; }

        /// <summary>
        /// 关卡 id
        /// </summary>
        [Key(5)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 子关卡索引
        /// </summary>
        [Key(6)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// 状态机数据
        /// </summary>
        [Key(7)] public List<AffectEntry> Affects { get; set; } = [];
    }
}
