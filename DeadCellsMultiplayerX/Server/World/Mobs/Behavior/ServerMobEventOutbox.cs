namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior;

using DeadCellsMultiplayerX.Contracts.MobBehavior;

/// <summary>
/// 服务端 mob 出站事件队列实现。
/// </summary>
internal sealed class ServerMobEventOutbox : IServerMobEventOutbox
{
    private readonly List<MobModifierEvent> pending = [];

    /// <inheritdoc />
    public void Enqueue(MobModifierEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        this.pending.Add(evt);
    }

    /// <inheritdoc />
    public IReadOnlyList<MobModifierEvent> Peek() => this.pending.ToArray();

    /// <inheritdoc />
    public void Commit(int count) => this.pending.RemoveRange(0, count);

    /// <inheritdoc />
    public void Clear() => this.pending.Clear();
}
