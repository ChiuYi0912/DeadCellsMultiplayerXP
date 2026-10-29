namespace DeadCellsMultiplayerX.Server.World.Ghosts.Pipeline
{
    /// <summary>
    /// Ghost 动作管线中的一个阶段
    /// </summary>
    internal interface IActionStage
    {
        /// <summary>
        /// 处理上下文,返回拒绝结果时管线停止
        /// </summary>
        StageResult Process(ActionContext context);
    }


    internal readonly record struct StageResult
    {
        public StageResult(bool isRejected, Rejection rejection)
        {
            IsRejected = isRejected;
            Rejection = rejection;
        }

        /// <summary>
        /// 是否拒绝当前动作
        /// </summary>
        public bool IsRejected { get; }

        /// <summary>
        /// 拒绝原因 最终为 none
        /// </summary>
        public Rejection Rejection { get; }

        /// <summary>
        /// 继续执行下一个阶段
        /// </summary>
        public static StageResult Continue
            => new(false, Rejection.None);

        /// <summary>
        /// 停止管线并记录拒绝原因
        /// </summary>
        public static StageResult Reject(Rejection rejection)
            => new(true, rejection);
    }

    /// <summary>
    /// 动作被拒绝的原因
    /// </summary>
    internal enum Rejection
    {
        None,
        InvalidRequest,
        EpochMismatch,
        InvalidSlot,
        InvalidItem,
        InvalidRatio,
        InvalidActionId,
        StaleAction,
        GameUnavailable,
        NoClientState,
        HeroDead,
        LevelMismatch,
        LevelUnavailable,
        TooFar,
        GhostUnavailable,
        ExecutionRejected,
    }
}
