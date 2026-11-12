namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior;

using dc.en;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Session;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;

/// <summary>
/// 服务端修改器绑定上下文与依赖注入入口
/// </summary>
internal sealed class ServerMobModifierContext(
    Mob mob,
    uint netId,
    long tick,
    ServerSession session,
    ServerTeamManager teams,
    IServerMobAttackRouter attacks,
    IServerMobEventOutbox events) : IMobModifierContext
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
    /// 服务端会话。
    /// </summary>
    public ServerSession Session { get; } = session;

    /// <summary>
    /// 服务端通用实体注册表。
    /// </summary>
    public ServerEntityRegistry Entities => this.Session.Entities;

    /// <summary>
    /// Ghost 队伍管理器。
    /// </summary>
    public ServerTeamManager Teams { get; } = teams;

    /// <summary>
    /// 服务端 mob 攻击释放路由。
    /// </summary>
    public IServerMobAttackRouter Attacks { get; } = attacks;

    /// <summary>
    /// 服务端 mob 事件总线。
    /// </summary>
    public IServerMobEventOutbox Events { get; } = events;

    /// <inheritdoc />
    public T GetService<T>() where T : class
    {
        if (typeof(T) == typeof(ServerSession))
        {
            return (T)(object)this.Session;
        }

        if (typeof(T) == typeof(ServerEntityRegistry))
        {
            return (T)(object)this.Entities;
        }

        if (typeof(T) == typeof(ServerTeamManager))
        {
            return (T)(object)this.Teams;
        }

        if (typeof(T) == typeof(IServerMobAttackRouter))
        {
            return (T)(object)this.Attacks;
        }

        if (typeof(T) == typeof(IServerMobEventOutbox))
        {
            return (T)(object)this.Events;
        }

        return null!;
    }
}
