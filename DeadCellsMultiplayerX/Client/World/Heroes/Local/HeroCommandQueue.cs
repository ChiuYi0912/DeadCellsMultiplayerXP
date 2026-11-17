namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;

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
    /// 返回本帧要附带的未确认命令快照，不出队，供后续上传帧继续重发。
    /// </summary>
    public IReadOnlyList<HeroCommand> GetPending(long nowMs)
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

    /// <summary>
    /// 确认已验证帧中属于本玩家、当前 epoch 的命令结果。
    /// 执行成功与拒绝均为最终结果，世界事件不确认命令。
    /// </summary>
    public void Acknowledge(WorldFrame frame, string localGuid)
    {
        if (frame.Epoch != this.epoch || frame.HeroOutcomes == null)
        {
            return;
        }

        foreach (var outcome in frame.HeroOutcomes)
        {
            if (outcome != null
                && outcome.Epoch == this.epoch
                && string.Equals(outcome.CasterGuid, localGuid, StringComparison.Ordinal)
                && outcome.Cause == HeroOutcomeCause.Command
                && outcome.CommandId > 0)
            {
                _ = this.pending.Remove(outcome.CommandId);
            }
        }
    }

    /// <summary>
    /// 返回 true 表示进入更新的 epoch，命令 id 需要从 0 重新开始。
    /// </summary>
    public bool ObserveEpoch(int value)
    {
        if (value <= this.epoch)
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
