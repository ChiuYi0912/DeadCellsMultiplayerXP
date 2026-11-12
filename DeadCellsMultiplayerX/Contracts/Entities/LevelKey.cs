namespace DeadCellsMultiplayerX.Contracts.Entities;

/// <summary>
/// Stable identity of a game level: level id + sub-level index.
/// </summary>
public readonly record struct LevelKey(string LevelId, int SubLevelIndex)
{
    public bool IsValid
        => this.SubLevelIndex >= 0 && !string.IsNullOrEmpty(this.LevelId);

    public static LevelKey Make(string levelId, int subLevelIndex)
        => new(levelId, subLevelIndex);

    public override string ToString()
        => $"{this.LevelId}#{this.SubLevelIndex}";
}
