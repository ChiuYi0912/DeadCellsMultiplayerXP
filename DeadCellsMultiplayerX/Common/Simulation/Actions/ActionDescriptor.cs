using DeadCellsMultiplayerX.Common.Data.Ghosts;

namespace DeadCellsMultiplayerX.Common.Simulation.Actions
{
    public sealed class ActionDescriptor
    {
        public ActionDescriptor(
            HeroActionKind kind,
            SlotPolicy slotPolicy,
            bool coalescable = false,
            int maxPerSecond = 0)
        {
            Kind = kind;
            SlotPolicy = slotPolicy;
            Coalescable = coalescable;
            MaxPerSecond = maxPerSecond;
        }

        public HeroActionKind Kind { get; }

        public SlotPolicy SlotPolicy { get; }

        public bool Coalescable { get; }

        public int MaxPerSecond { get; }
    }
}
