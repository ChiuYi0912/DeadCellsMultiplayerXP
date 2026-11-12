namespace DeadCellsMultiplayerX.Contracts.Entities
{
    /// <summary>
    /// 实体移除原因
    /// </summary>
    public enum EntityDespawnReason : byte
    {
        Removed = 0,
        Killed = 1,
        PickedUp = 2,
        Reconciled = 3,
        LevelUnloaded = 4,
    }
}
