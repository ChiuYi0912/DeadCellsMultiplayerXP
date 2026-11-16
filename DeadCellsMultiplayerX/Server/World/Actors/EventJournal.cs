namespace DeadCellsMultiplayerX.Server.World.Actors;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 待广播的 Ghost 动作结果队列
/// </summary>
internal sealed class EventJournal
{
    private readonly List<HeroOutcome> pending = [];

    /// <summary>
    /// 记录一次动作结果
    /// </summary>
    public void Record(HeroOutcome outcome)
        => this.pending.Add(outcome);

    /// <summary>
    /// 取出本帧全部结果并清空
    /// </summary>
    public List<HeroOutcome> Drain()
    {
        if (this.pending.Count == 0)
        {
            return [];
        }

        var result = new List<HeroOutcome>(this.pending);
        this.pending.Clear();
        return result;
    }

    /// <summary>
    /// 世界重置时丢弃未广播事件
    /// </summary>
    public void Clear() => this.pending.Clear();
}
