namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// 服务端最终战斗裁决类型。
    /// </summary>
    public enum CombatResolutionKind : byte
    {
        /// <summary>
        /// 命中。
        /// </summary>
        Hit = 0,

        /// <summary>
        /// 被盾反。
        /// </summary>
        Parried = 1,

        /// <summary>
        /// 被闪避。
        /// </summary>
        Dodged = 2,

        /// <summary>
        /// 未命中。
        /// </summary>
        Missed = 3,

        /// <summary>
        /// 动作被取消。
        /// </summary>
        Cancelled = 4,

        /// <summary>
        /// 对之前预测结果的纠正。
        /// </summary>
        Corrected = 5,
    }

    /// <summary>
    /// 服务端最终裁决，用于客户端对账/回滚。
    /// </summary>
    public sealed class CombatResolution
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// 命中票据 id。
        /// </summary>
        public long HitId { get; init; }

        /// <summary>
        /// 动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 被攻击玩家 guid。
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 裁决类型。
        /// </summary>
        public CombatResolutionKind Kind { get; init; }

        /// <summary>
        /// 最终伤害。
        /// </summary>
        public int FinalDamage { get; init; }

        /// <summary>
        /// 盾反反击伤害。
        /// </summary>
        public int CounterDamage { get; init; }

        /// <summary>
        /// 眩晕时长（秒）。
        /// </summary>
        public double StunSeconds { get; init; }

        /// <summary>
        /// 击退方向。
        /// </summary>
        public int BumpDir { get; init; }

        /// <summary>
        /// 服务端裁决 Tick。
        /// </summary>
        public long AuthorityTick { get; init; }

        /// <summary>
        /// 是否纠正之前的预测结果。
        /// </summary>
        public bool CorrectsPrevious { get; init; }

        /// <summary>
        /// 裁决说明/调试信息。
        /// </summary>
        public string Reason { get; init; } = string.Empty;
    }
}
