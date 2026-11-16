namespace DeadCellsMultiplayerX.Simulation.Actions;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 每个客户端的动作结果账本。
/// 有界缓存最近处理过的 CommandId，重复命令直接返回原结果。
/// </summary>
public sealed class CommandLedger
{
    private const int CAPACITY = 128;

    private readonly Dictionary<long, HeroOutcome> outcomes = [];
    private readonly Queue<long> order = new(CAPACITY);

    public bool TryGetCached(long commandId, out HeroOutcome? outcome)
        => this.outcomes.TryGetValue(commandId, out outcome);

    public void Record(long commandId, HeroOutcome outcome)
    {
        if (this.outcomes.ContainsKey(commandId))
        {
            this.outcomes[commandId] = outcome;
            return;
        }

        this.outcomes[commandId] = outcome;
        this.order.Enqueue(commandId);

        while (this.order.Count > CAPACITY)
        {
            _ = this.outcomes.Remove(this.order.Dequeue());
        }
    }

    public void Clear()
    {
        this.outcomes.Clear();
        this.order.Clear();
    }
}
