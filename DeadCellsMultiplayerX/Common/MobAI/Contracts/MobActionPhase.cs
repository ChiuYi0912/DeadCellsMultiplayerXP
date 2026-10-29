namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// Mob 动作阶段。
    /// </summary>
    public enum MobActionPhase : byte
    {
        /// <summary>
        /// 前摇/蓄力。
        /// </summary>
        Windup = 0,

        /// <summary>
        /// 攻击生效。
        /// </summary>
        Active = 1,

        /// <summary>
        /// 后摇。
        /// </summary>
        Recovery = 2,

        /// <summary>
        /// 已取消。
        /// </summary>
        Cancelled = 3,

        /// <summary>
        /// 已完成。
        /// </summary>
        Completed = 4,
    }

    /// <summary>
    /// 服务端对动作阶段的纠正/确认事件。
    /// </summary>
    public sealed class MobActionPhaseUpdate
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// 动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 当前阶段。
        /// </summary>
        public MobActionPhase Phase { get; init; }

        /// <summary>
        /// 服务端 Tick。
        /// </summary>
        public long Tick { get; init; }

        /// <summary>
        /// 取消原因；仅在 <see cref="MobActionPhase.Cancelled"/> 时有效。
        /// </summary>
        public MobActionCancelReason CancelReason { get; init; }
    }
}
