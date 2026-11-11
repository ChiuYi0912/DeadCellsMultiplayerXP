namespace DeadCellsMultiplayerX.Common.Data.Ghosts
{
    public enum ShieldActionKind : byte
    {
        /// <summary>
        /// 开始举盾
        /// </summary>
        HoldStart = 0,

        /// <summary>
        /// 松开盾牌
        /// </summary>
        HoldEnd = 1,

        /// <summary>
        /// 弹反窗口开始
        /// </summary>
        ParryStart = 2,

        /// <summary>
        /// 弹反窗口结束
        /// </summary>
        ParryEnd = 3,

        /// <summary>
        /// 成功格挡
        /// </summary>
        Block = 4,

        /// <summary>
        /// 弹反反击
        /// </summary>
        Counter = 5,
    }
}
