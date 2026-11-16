namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 远端动作结果的呈现时间线。
/// 结果按 ServerTimeMs + 插值延迟排序，与可见 Hero 的插值节奏对齐。
/// </summary>
internal sealed class HeroOutcomeTimeline
{
    private readonly SortedDictionary<long, List<Pending>> pending = [];

    public void Enqueue(
        WorldFrame frame,
        HeroOutcome outcome,
        long presentAtMs)
    {
        if (!this.pending.TryGetValue(presentAtMs, out var list))
        {
            this.pending[presentAtMs] = list = [];
        }

        list.Add(new Pending(frame, outcome));
    }

    public void Drain(
        long nowMs,
        Action<WorldFrame, HeroOutcome> present)
    {
        while (this.pending.Count > 0)
        {
            var first = this.pending.First();
            if (first.Key > nowMs)
            {
                break;
            }

            _ = this.pending.Remove(first.Key);

            foreach (var item in first.Value)
            {
                present(item.Frame, item.Outcome);
            }
        }
    }

    public void Clear() => this.pending.Clear();

    private readonly record struct Pending(
        WorldFrame Frame,
        HeroOutcome Outcome);
}
