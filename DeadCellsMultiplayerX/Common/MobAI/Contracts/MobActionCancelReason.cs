namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// Mob 动作取消原因。
    /// </summary>
    public enum MobActionCancelReason : byte
    {
        /// <summary>
        /// 未取消。
        /// </summary>
        None = 0,

        /// <summary>
        /// 被玩家打断。
        /// </summary>
        Interrupted = 1,

        /// <summary>
        /// 硬直达到阈值。
        /// </summary>
        Staggered = 2,

        /// <summary>
        /// 目标丢失。
        /// </summary>
        TargetLost = 3,

        /// <summary>
        /// mob 被移除。
        /// </summary>
        Despawned = 4,

        /// <summary>
        /// 被新的动作替换。
        /// </summary>
        Replaced = 5,
    }
}
