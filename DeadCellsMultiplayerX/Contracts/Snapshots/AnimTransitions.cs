using MessagePack;
namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    [MessagePackObject]
    public class AnimTransitions
    {
        [Key(0)] public string? Anim { get; set; } = null;
        [Key(1)] public string? From { get; set; } = null;
        [Key(2)] public string? To { get; set; } = null;
        [Key(3)] public bool? reverse { get; set; } = null;
        [Key(4)] public double? speed { get; set; } = null;

        public AnimTransitions() { }
    }
}
