namespace DeadCellsMultiplayerX.Simulation.Actions;

using DeadCellsMultiplayerX.Contracts.Actions;

public sealed class ActionDescriptor(
    HeroActionKind kind,
    SlotPolicy slotPolicy,
    bool coalescable = false,
    int maxPerSecond = 0)
{
    public HeroActionKind Kind { get; } = kind;

    public SlotPolicy SlotPolicy { get; } = slotPolicy;

    public bool Coalescable { get; } = coalescable;

    public int MaxPerSecond { get; } = maxPerSecond;
}
