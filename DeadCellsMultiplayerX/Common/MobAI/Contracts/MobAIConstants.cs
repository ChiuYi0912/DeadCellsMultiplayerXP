namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// Mob AI 协议与时间模型的全局常量。
    /// 修改这些值时必须同步提升 <see cref="ProtocolVersion"/>。
    /// </summary>
    public static class MobAIConstants
    {
        /// <summary>
        /// MobActionPlan / DefenseReport / CombatResolution 的协议版本。
        /// </summary>
        public const int ProtocolVersion = 1;

        /// <summary>
        /// 服务端逻辑 Tick 频率。
        /// </summary>
        public const int ServerTickRate = 60;

        /// <summary>
        /// 世界快照广播频率。
        /// </summary>
        public const int SnapshotRate = 20;

        /// <summary>
        /// 客户端渲染插值延迟（毫秒）。
        /// </summary>
        public const int InterpolationDelayMs = 100;

        /// <summary>
        /// 命中判定与伤害提交之间允许的防御宽限 Tick 数。
        /// </summary>
        public const int DefenseGraceTicks = 6;

        /// <summary>
        /// 服务端战斗状态最大回溯时间（毫秒）。
        /// </summary>
        public const int MaxRewindMs = 500;

        /// <summary>
        /// 服务端输入缓冲保留时间（毫秒）。
        /// </summary>
        public const int InputBufferMs = 250;

        /// <summary>
        /// 计划最多提前多少毫秒广播。
        /// </summary>
        public const int MaxPlanLeadMs = 1000;

        /// <summary>
        /// 单个 mob 同时允许存在的权威动作数量。
        /// </summary>
        public const int MaxActivePlansPerMob = 1;

        /// <summary>
        /// 默认硬直阈值。
        /// </summary>
        public const int DefaultStaggerThreshold = 100;
    }
}
