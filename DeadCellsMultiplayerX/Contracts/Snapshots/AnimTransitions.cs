namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using MessagePack;

[MessagePackObject]
public class AnimTransitions
{
    [Key(0)] public string? Anim { get; set; } = null;
    [Key(1)] public string? From { get; set; } = null;
    [Key(2)] public string? To { get; set; } = null;
    [Key(3)] public bool? Reverse { get; set; } = null;
    [Key(4)] public double? Speed { get; set; } = null;

    public AnimTransitions() { }
}
