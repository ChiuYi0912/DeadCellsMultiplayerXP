using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端确认的一次 mob 命中 Ghost
    /// 服务端只判定命中，客户端结算生命死亡
    /// </summary>
    [MessagePackObject]
    public class GhostHitEvent
    {
        /// <summary>
        /// 世界重置代号
        /// </summary>
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// epoch 内单调递增的命中序号，用于客户端去重
        /// </summary>
        [Key(1)] public long HitId { get; set; }

        /// <summary>
        /// 被命中的 Ghost owner
        /// </summary>
        [Key(2)] public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// 攻击者 mob 的 NetId
        /// </summary>
        [Key(3)] public uint AttackerNetId { get; set; }

        /// <summary>
        /// 服务端计算出的最终伤害
        /// </summary>
        [Key(4)] public int Damage { get; set; }

        /// <summary>
        /// 服务端时间戳
        /// </summary>
        [Key(5)] public double ServerTimeMs { get; set; }

        /// <summary>
        /// 命中发生时 Ghost 所在关卡
        /// </summary>
        [Key(6)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 命中发生时 Ghost 所在子关卡
        /// </summary>
        [Key(7)] public int SubLevelIndex { get; set; } = -1;
    }
}
