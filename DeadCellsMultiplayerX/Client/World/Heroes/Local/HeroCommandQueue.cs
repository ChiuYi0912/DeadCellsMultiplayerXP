namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 未确认动作命令队列。
/// 命令随 ClientWorldFrame 重发，收到服务端 Outcome 后移除。
/// </summary>
internal sealed class HeroCommandQueue
{
    private const int MAX_PENDING = 64;
    private const long RETENTION_MS = 5000;

    private readonly SortedDictionary<long, PendingCommand> pending = [];

    private int epoch = int.MinValue;
    private long nextCommandId;

    public int PendingCount => this.pending.Count;

    public long NextCommandId() => ++this.nextCommandId;

    public void Enqueue(HeroCommand command)
    {
        if (command == null || command.CommandId <= 0)
        {
            return;
        }

        this.pending[command.CommandId] = new PendingCommand(
            command,
            command.ClientTimeMs + RETENTION_MS);

        while (this.pending.Count > MAX_PENDING)
        {
            this.pending.Remove(this.pending.Keys.First());
        }
    }

    /// <summary>
    /// 取出本帧要附带的所有未确认命令。
    /// </summary>
    public IReadOnlyList<HeroCommand> DrainPending(long nowMs)
    {
        this.Prune(nowMs);

        if (this.pending.Count == 0)
        {
            return [];
        }

        var result = new List<HeroCommand>(this.pending.Count);
        foreach (var entry in this.pending.Values)
        {
            result.Add(entry.Command);
        }

        return result;
    }

    public void Acknowledge(long commandId)
    {
        if (commandId > 0)
        {
            _ = this.pending.Remove(commandId);
        }
    }

    /// <summary>
    /// 返回 true 表示 epoch 变化，命令 id 需要从 0 重新开始。
    /// </summary>
    public bool ObserveEpoch(int value)
    {
        if (value == this.epoch)
        {
            return false;
        }

        this.epoch = value;
        this.pending.Clear();
        this.nextCommandId = 0;
        return true;
    }

    public void Clear()
    {
        this.pending.Clear();
        this.nextCommandId = 0;
    }

    private void Prune(long nowMs)
    {
        while (this.pending.Count > 0)
        {
            var first = this.pending.First();
            if (first.Value.ExpiresAtMs > nowMs)
            {
                break;
            }

            _ = this.pending.Remove(first.Key);
        }
    }

    private readonly record struct PendingCommand(
        HeroCommand Command,
        long ExpiresAtMs);
}
