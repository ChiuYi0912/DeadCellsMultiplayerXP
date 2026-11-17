namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 远端动作结果的呈现时间线。
/// 结果按 ServerTimeMs + 插值延迟排序，与可见 Hero 的插值节奏对齐。
/// </summary>
internal sealed class HeroOutcomeTimeline
{
    private readonly SortedDictionary<long, List<HeroOutcome>> pending = [];

    public void Enqueue(
        HeroOutcome outcome,
        long presentAtMs)
    {
        if (!this.pending.TryGetValue(presentAtMs, out var list))
        {
            this.pending[presentAtMs] = list = [];
        }

        list.Add(outcome);
    }

    public void Drain(
        long nowMs,
        Action<HeroOutcome> present)
    {
        while (this.pending.Count > 0)
        {
            var first = this.pending.First();
            if (first.Key > nowMs)
            {
                break;
            }

            var outcome = first.Value[0];
            first.Value.RemoveAt(0);
            if (first.Value.Count == 0)
            {
                this.pending.Remove(first.Key);
            }
            // Re-read the queue after presentation: native callbacks may remove
            // this actor or clear the world while a bucket is being drained.
            present(outcome);
        }
    }

    public void Clear() => this.pending.Clear();

    public void RemoveHero(string guid)
    {
        foreach (var (time, outcomes) in this.pending.ToArray())
        {
            outcomes.RemoveAll(outcome => string.Equals(outcome.CasterGuid, guid, StringComparison.Ordinal));
            if (outcomes.Count == 0)
            {
                this.pending.Remove(time);
            }
        }
    }
}
