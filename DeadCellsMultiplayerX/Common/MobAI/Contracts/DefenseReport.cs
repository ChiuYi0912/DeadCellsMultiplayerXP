namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// 防御输入类型。
    /// </summary>
    public enum DefenseKind : byte
    {
        /// <summary>
        /// 无防御。
        /// </summary>
        None = 0,

        /// <summary>
        /// 闪避。
        /// </summary>
        Dodge = 1,

        /// <summary>
        /// 盾反。
        /// </summary>
        Parry = 2,

        /// <summary>
        /// 完美盾反。
        /// </summary>
        PerfectParry = 3,

        /// <summary>
        /// 普通格挡。
        /// </summary>
        Block = 4,
    }

    /// <summary>
    /// 客户端上报的防御输入。
    /// 服务端根据 InputTick 回溯验证是否落在防御窗口内。
    /// </summary>
    public sealed class DefenseReport
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// 对应的动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 对应的命中票据；0 表示客户端还不知道具体命中。
        /// </summary>
        public long HitId { get; init; }

        /// <summary>
        /// 防御者 guid。
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 防御者 NetId。
        /// </summary>
        public uint TargetNetId { get; init; }

        /// <summary>
        /// 客户端输入发生的 Tick。
        /// </summary>
        public long InputTick { get; init; }

        /// <summary>
        /// 客户端输入发生的时间（毫秒）。
        /// </summary>
        public double InputTimeMs { get; init; }

        /// <summary>
        /// 防御类型。
        /// </summary>
        public DefenseKind Kind { get; init; }

        /// <summary>
        /// 输入时的朝向。
        /// </summary>
        public int Dir { get; init; }

        /// <summary>
        /// 客户端输入序号，用于去重/排序。
        /// </summary>
        public long InputSequence { get; init; }
    }
}
