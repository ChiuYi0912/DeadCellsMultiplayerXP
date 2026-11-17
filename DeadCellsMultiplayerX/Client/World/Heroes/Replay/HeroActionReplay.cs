namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Time;

/// <summary>Accepts authoritative outcomes once and schedules presentation on the render timeline.</summary>
internal sealed class HeroActionReplay(string localGuid, Action<HeroOutcome> present) : IDisposable
{
    private const long MAX_EVENT_AGE_MS = 2000;
    private readonly HeroOutcomeTimeline timeline = new();
    private readonly HashSet<(string Caster, long OutcomeId)> appliedActions = [];
    private Action<HeroOutcome>? present = present ?? throw new ArgumentNullException(nameof(present));
    private int lastEpoch = int.MinValue;
    private bool disposed;

    public void BeginFrame(WorldFrame frame)
    {
        if (this.disposed || frame == null || frame.Epoch == this.lastEpoch)
        {
            return;
        }
        this.Clear();
        this.lastEpoch = frame.Epoch;
    }

    public void ApplyFrame(WorldFrame frame)
    {
        if (this.disposed || frame == null)
        {
            return;
        }
        this.BeginFrame(frame);
        if (this.appliedActions.Count > 8192)
        {
            this.appliedActions.Clear();
        }
        if (frame.HeroOutcomes == null)
        {
            return;
        }
        foreach (var outcome in frame.HeroOutcomes)
        {
            if (outcome == null || outcome.Epoch != frame.Epoch
                || outcome.Status != HeroOutcomeStatus.Executed)
            {
                continue;
            }
            if (!this.appliedActions.Add((outcome.CasterGuid, outcome.OutcomeId)))
            {
                continue;
            }
            if (string.Equals(outcome.CasterGuid, localGuid, StringComparison.Ordinal)
                || frame.ServerTimeMs - outcome.ServerTimeMs > MAX_EVENT_AGE_MS
                || outcome.Slot is < 0 or > 1)
            {
                continue;
            }
            this.timeline.Enqueue(outcome, outcome.ServerTimeMs + SnapshotTiming.INTERPOLATION_DELAY_MS);
        }
    }

    public void Tick(long nowMs)
    {
        if (!this.disposed && this.present != null)
        {
            this.timeline.Drain(nowMs, this.present);
        }
    }

    public void RemoveHero(string guid) => this.timeline.RemoveHero(guid);

    public void Clear()
    {
        this.timeline.Clear();
        this.appliedActions.Clear();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }
        this.disposed = true;
        this.present = null;
        this.Clear();
    }
}
