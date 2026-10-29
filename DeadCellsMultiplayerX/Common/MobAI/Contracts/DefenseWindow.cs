namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// 盾反/格挡/闪避的有效 Tick 窗口。
    /// </summary>
    public readonly record struct DefenseWindow(long StartTick, long EndTick)
    {
        /// <summary>
        /// 指定 Tick 是否落在窗口内。
        /// </summary>
        public bool Contains(long tick) => tick >= StartTick && tick <= EndTick;
    }
}
