namespace DeadCellsMultiplayerX.Common.Simulation.Actions
{
    public enum SlotPolicy : byte
    {
        /// <summary>
        /// 任意槽位，具体由动作自身校验。
        /// </summary>
        Any = 0,

        /// <summary>
        /// 只允许武器槽。
        /// </summary>
        Weapon = 1,

        /// <summary>
        /// 只允许主动技能槽。
        /// </summary>
        Active = 2,

        /// <summary>
        /// 只允许项链槽（当前固定 slot 0）。
        /// </summary>
        TalismanOnly = 3,
    }
}
