using dc.en;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.MobAI.Events;
using DeadCellsMultiplayerX.Server.World.Ghosts;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
using DeadCellsMultiplayerX.Server.World.Hubs;

namespace DeadCellsMultiplayerX.Server.MobAI.Modifiers
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
            ServerGhostManager ghosts,
            ServerTeamManager teams,
            IServerMobEventBus events)
        {
            Mob = mob;
            NetId = netId;
            Tick = tick;
            Session = session;
            Ghosts = ghosts;
            Teams = teams;
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
        /// 服务端 mob 注册表。
        /// </summary>
        public ServerMobRegistry MobRegistry => Session.MobRegistry;

        /// <summary>
        /// Ghost 队伍管理器。
        /// </summary>
        public ServerTeamManager Teams { get; }

        /// <summary>
        /// Ghost 入口。
        /// </summary>
        public ServerGhostManager Ghosts { get; }

        /// <summary>
        /// 服务端 mob 事件总线。
        /// </summary>
        public IServerMobEventBus Events { get; }

        /// <inheritdoc />
        public T? GetService<T>() where T : class
        {
            if (typeof(T) == typeof(ServerSession))
                return (T)(object)Session;

            if (typeof(T) == typeof(ServerMobRegistry))
                return (T)(object)MobRegistry;

            if (typeof(T) == typeof(ServerTeamManager))
                return (T)(object)Teams;

            if (typeof(T) == typeof(ServerGhostManager))
                return (T)(object)Ghosts;

            if (typeof(T) == typeof(IServerMobEventBus))
                return (T)(object)Events;

            return null;
        }
    }
}
