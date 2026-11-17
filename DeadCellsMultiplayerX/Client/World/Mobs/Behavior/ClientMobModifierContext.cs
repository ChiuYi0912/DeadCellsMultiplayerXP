namespace DeadCellsMultiplayerX.Client.World.Mobs.Behavior;

using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;

/// <summary>
/// 客户端修改器绑定上下文与依赖注入入口
/// </summary>
internal sealed class ClientMobModifierContext(
    Mob mob,
    uint netId,
    long tick,
    GuestGameSession session,
    RemoteHeroManager remoteHeroes) : IMobModifierContext
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
    public GuestGameSession Session { get; } = session;

    public RemoteHeroManager RemoteHeroes { get; } = remoteHeroes;

    /// <inheritdoc />
    public T GetService<T>() where T : class
    {
        if (typeof(T) == typeof(GuestGameSession))
        {
            return (T)(object)this.Session;
        }

        if (typeof(T) == typeof(RemoteHeroManager))
        {
            return (T)(object)this.RemoteHeroes;
        }

        return null!;
    }
}
