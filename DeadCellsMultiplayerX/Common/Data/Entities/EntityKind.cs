namespace DeadCellsMultiplayerX.Common.Data.Entities
{
    /// <summary>
    /// 通用复制实体类型
    /// 只允许在末尾追加，禁止重排或复用旧值
    /// </summary>
    public enum EntityKind : byte
    {
        Unknown = 0,
        Mob = 1,
        Pet = 2,
        Boss = 3,
        Loot = 4,
        Interactive = 5,
        Door = 6,
    }

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

    public static class LevelKeys
    {
        public static string Make(string levelId, int subLevelIndex)
            => $"{levelId}#{subLevelIndex}";
    }
}
