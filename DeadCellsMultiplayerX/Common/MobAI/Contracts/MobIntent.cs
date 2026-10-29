namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// Mob 大脑输出的高层意图。
    /// 意图只描述“想做什么”，不包含技能执行细节和命中结果。
    /// </summary>
    public sealed class MobIntent
    {
        /// <summary>
        /// epoch 内单调递增的意图 id。
        /// </summary>
        public long IntentId { get; init; }

        /// <summary>
        /// 发出意图的 mob。
        /// </summary>
        public uint MobNetId { get; init; }

        /// <summary>
        /// 目标技能 id。
        /// </summary>
        public string SkillId { get; init; } = string.Empty;

        /// <summary>
        /// 目标玩家/实体；0 表示无目标。
        /// </summary>
        public uint TargetNetId { get; init; }

        /// <summary>
        /// 目标玩家 guid；无目标时为空。
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 瞄准方向。
        /// </summary>
        public int AimDir { get; init; }

        /// <summary>
        /// 瞄准点 X。
        /// </summary>
        public double AimX { get; init; }

        /// <summary>
        /// 瞄准点 Y。
        /// </summary>
        public double AimY { get; init; }

        /// <summary>
        /// 多个意图冲突时的优先级。
        /// </summary>
        public int Priority { get; init; }

        /// <summary>
        /// 产生意图的 Tick。
        /// </summary>
        public long CreatedTick { get; init; }
    }
}
