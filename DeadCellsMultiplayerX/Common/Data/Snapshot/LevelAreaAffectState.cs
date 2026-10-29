using System.Collections.Generic;
using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端权威的动态关卡区域状态
    /// 对应 LevelAreaAffect（毒水/火焰/毒云等）
    /// </summary>
    [MessagePackObject]
    public class LevelAreaAffectState
    {
        [Key(0)] public string LevelId { get; set; } = string.Empty;

        [Key(1)] public int SubLevelIndex { get; set; } = -1;

        [Key(2)] public int Cx { get; set; }

        [Key(3)] public int Cy { get; set; }

        [Key(4)] public int A { get; set; }

        [Key(5)] public double ADurationS { get; set; }

        [Key(6)] public double AValue { get; set; }

        /// <summary>
        /// 服务端采集时的剩余秒数
        /// </summary>
        [Key(7)] public double RemainingSec { get; set; }

        /// <summary>
        /// 服务端采集时刻
        /// </summary>
        [Key(8)] public double ServerTimeSec { get; set; }

        [Key(9)] public List<string> Affixes { get; set; } = [];
    }
}
