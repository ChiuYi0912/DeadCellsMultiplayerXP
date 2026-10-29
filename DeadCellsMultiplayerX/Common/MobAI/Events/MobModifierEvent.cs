namespace DeadCellsMultiplayerX.Common.MobAI.Events
{
    /// <summary>
    /// 修改器事件类型
    /// </summary>
    public enum MobModifierEventKind
    {
        /// <summary>
        /// 动作开始
        /// </summary>
        ActionStarted = 0,

        /// <summary>
        /// 动作阶段变化
        /// </summary>
        ActionPhase = 1,

        /// <summary>
        /// 动作结束
        /// </summary>
        ActionEnded = 2,

        /// <summary>
        /// 攻击命中
        /// </summary>
        AttackHit = 3,

        /// <summary>
        /// mob 死亡
        /// </summary>
        MobDied = 4,

        /// <summary>
        /// 自定义事件
        /// </summary>
        Custom = 5,
    }

    /// <summary>
    /// 修改器产生的纯数据事件
    /// 当前只用于进程内事件总线，尚未写入 WorldFrame
    /// </summary>
    public sealed class MobModifierEvent
    {
        /// <summary>
        /// 世界重置代号
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// 动作 id
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 产生事件的 mob
        /// </summary>
        public uint MobNetId { get; init; }

        /// <summary>
        /// 事件类型
        /// </summary>
        public MobModifierEventKind Kind { get; init; }

        /// <summary>
        /// 技能 id；非技能事件可为空
        /// </summary>
        public string SkillId { get; init; } = string.Empty;

        /// <summary>
        /// 目标玩家 guid；无目标时为空
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 服务端时间戳
        /// </summary>
        public long ServerTimeMs { get; init; }

        /// <summary>
        /// 事件发生时所在关卡
        /// </summary>
        public string LevelId { get; init; } = string.Empty;

        /// <summary>
        /// 事件发生时所在子关卡
        /// </summary>
        public int SubLevelIndex { get; init; } = -1;

        /// <summary>
        /// 预留的自定义负载
        /// </summary>
        public string? Data { get; init; }
    }
}
