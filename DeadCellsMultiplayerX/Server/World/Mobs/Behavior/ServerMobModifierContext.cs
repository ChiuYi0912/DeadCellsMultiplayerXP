using dc.en;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Session;
namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior
{
    /// <summary>
    /// 服务端修改器绑定上下文与依赖注入入口
    /// </summary>
    internal sealed class ServerMobModifierContext : IMobModifierContext
    {
        public ServerMobModifierContext(
            Mob mob,
            uint netId,
            long tick,
            ServerSession session,
            ServerTeamManager teams,
            IServerMobAttackRouter attacks,
            IServerMobEventQueue events)
        {
            Mob = mob;
            NetId = netId;
            Tick = tick;
            Session = session;
            Teams = teams;
            Attacks = attacks;
            Events = events;
        }

        /// <inheritdoc />
        public Mob Mob { get; }

        /// <inheritdoc />
        public uint NetId { get; }

        /// <inheritdoc />
        public System.Type MobType => Mob.GetType();

        /// <inheritdoc />
        public long Tick { get; }

        /// <summary>
        /// 服务端会话。
        /// </summary>
        public ServerSession Session { get; }

        /// <summary>
        /// 服务端通用实体注册表。
        /// </summary>
        public ServerEntityRegistry Entities => Session.Entities;

        /// <summary>
        /// Ghost 队伍管理器。
        /// </summary>
        public ServerTeamManager Teams { get; }

        /// <summary>
        /// 服务端 mob 攻击释放路由。
        /// </summary>
        public IServerMobAttackRouter Attacks { get; }

        /// <summary>
        /// 服务端 mob 事件总线。
        /// </summary>
        public IServerMobEventQueue Events { get; }

        /// <inheritdoc />
        public T GetService<T>() where T : class
        {
            if (typeof(T) == typeof(ServerSession))
                return (T)(object)Session;

            if (typeof(T) == typeof(ServerEntityRegistry))
                return (T)(object)Entities;

            if (typeof(T) == typeof(ServerTeamManager))
                return (T)(object)Teams;

            if (typeof(T) == typeof(IServerMobAttackRouter))
                return (T)(object)Attacks;

            if (typeof(T) == typeof(IServerMobEventQueue))
                return (T)(object)Events;

            return null!;
        }
    }
}
