namespace DeadCellsMultiplayerX.Server.World.Actors;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 待广播的 Ghost 动作事件队列
/// </summary>
internal sealed class EventJournal
{
    private readonly List<HeroActionEvent> pending = [];

    /// <summary>
    /// 记录一次已执行成功的事件
    /// </summary>
    public void Record(HeroActionEvent actionEvent)
        => this.pending.Add(actionEvent);

    /// <summary>
    /// 取出本帧全部事件并清空
    /// </summary>
    public List<HeroActionEvent> Drain()
    {
        if (this.pending.Count == 0)
        {
            return [];
        }

        var result = new List<HeroActionEvent>(this.pending);
        this.pending.Clear();
        return result;
    }

    /// <summary>
    /// 世界重置时丢弃未广播事件
    /// </summary>
    public void Clear() => this.pending.Clear();
}
