namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 待广播的 Ghost 动作结果队列
/// </summary>
internal sealed class HeroOutcomeJournal
{
    private readonly List<HeroOutcome> pending = [];

    /// <summary>
    /// 记录一次动作结果
    /// </summary>
    public void Record(HeroOutcome outcome)
        => this.pending.Add(outcome);

    /// <summary>
    /// 读取待广播结果，不消费队列
    /// </summary>
    public List<HeroOutcome> Peek() => [.. this.pending];

    /// <summary>消费成功构建帧包含的结果。</summary>
    public void Commit(int count) => this.pending.RemoveRange(0, count);

    /// <summary>
    /// 世界重置时丢弃未广播事件
    /// </summary>
    public void Clear() => this.pending.Clear();
}
