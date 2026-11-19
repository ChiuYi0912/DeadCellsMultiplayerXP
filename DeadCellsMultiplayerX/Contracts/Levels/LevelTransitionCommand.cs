namespace DeadCellsMultiplayerX.Contracts.Levels;

using MessagePack;

/// <summary>Server authorization to move from one world epoch to another.</summary>
[MessagePackObject]
public sealed class LevelTransitionCommand
{
    [Key(0)] public int FromEpoch { get; set; } = -1;
    [Key(1)] public int Epoch { get; set; } = -1;
    [Key(2)] public string LevelId { get; set; } = string.Empty;
}
