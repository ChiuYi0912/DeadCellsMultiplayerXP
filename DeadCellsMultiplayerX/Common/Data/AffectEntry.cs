using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data
{
    /// <summary>
    /// effect 层的通用身份：类型 + 实例 id + 值
    /// UniqId 只在单个实体内有意义 不同客户端可能撞号
    /// Val 用于区分 DifferentValue stacking 以及撞号但数值不同的层
    /// </summary>
    public readonly record struct AffectKey(int A, int UniqId, double Val);

    [MessagePackObject]
    public class AffectEntry
    {
        [Key(0)] public int OuterIndex { get; set; }   // affects[i]
        [Key(1)] public int InnerIndex { get; set; }   // affects[i][j]
        [Key(2)] public int A { get; set; }
        [Key(3)] public double T { get; set; }
        [Key(4)] public int UniqId { get; set; }
        [Key(5)] public double Val { get; set; }

        [Key(6)] public double StartSec { get; set; }   // 首次出现时间
        [Key(7)] public double EndSec { get; set; }     // 结束时间 StartSec + T / baseFps

        [Key(8)] public double RemainingSec { get; set; }  // 服务端采集时的剩余秒数
        [Key(9)] public double ServerTimeSec { get; set; } // 服务端采集时刻

        [IgnoreMember]
        public AffectKey Key => new(A, UniqId, Val);
    }
}