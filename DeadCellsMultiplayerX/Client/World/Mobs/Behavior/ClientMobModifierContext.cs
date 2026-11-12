namespace DeadCellsMultiplayerX.Client.World.Mobs.Behavior;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

/// <summary>
/// 客户端修改器绑定上下文与依赖注入入口
/// </summary>
internal sealed class ClientMobModifierContext(
    Mob mob,
    uint netId,
    long tick,
    GuestClientSession session,
    ClientReplayProxyManager remoteGhost) : IMobModifierContext
{

    /// <inheritdoc />
    public Mob Mob { get; } = mob;

    /// <inheritdoc />
    public uint NetId { get; } = netId;

    /// <inheritdoc />
    public Type MobType => this.Mob.GetType();

    /// <inheritdoc />
    public long Tick { get; } = tick;

    /// <summary>
    /// 客户端会话。
    /// </summary>
    public GuestClientSession Session { get; } = session;

    public ClientReplayProxyManager RemoteGhost { get; } = remoteGhost;

    /// <inheritdoc />
    public T GetService<T>() where T : class
    {
        if (typeof(T) == typeof(GuestClientSession))
        {
            return (T)(object)this.Session;
        }

        if (typeof(T) == typeof(ClientReplayProxyManager))
        {
            return (T)(object)this.RemoteGhost;
        }

        return null!;
    }
}
