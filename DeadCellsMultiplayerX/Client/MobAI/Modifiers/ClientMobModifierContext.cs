using dc.en;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.MobAI.Events;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;

namespace DeadCellsMultiplayerX.Client.MobAI.Modifiers
{
    /// <summary>
    /// 客户端修改器绑定上下文与依赖注入入口
    /// </summary>
    internal sealed class ClientMobModifierContext : IMobModifierContext
    {
        public ClientMobModifierContext(
            Mob mob,
            uint netId,
            long tick,
            GuestClientSession session,
            RemoteWorld world,
            IClientMobEventBus events)
        {
            Mob = mob;
            NetId = netId;
            Tick = tick;
            Session = session;
            World = world;
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
        /// 客户端会话。
        /// </summary>
        public GuestClientSession Session { get; }

        /// <summary>
        /// 客户端世界。
        /// </summary>
        public RemoteWorld World { get; }

        /// <summary>
        /// 客户端 mob 事件总线
        /// </summary>
        public IClientMobEventBus Events { get; }

        /// <inheritdoc />
        public T? GetService<T>() where T : class
        {
            if (typeof(T) == typeof(GuestClientSession))
                return (T)(object)Session;

            if (typeof(T) == typeof(RemoteWorld))
                return (T)(object)World;

            if (typeof(T) == typeof(IClientMobEventBus))
                return (T)(object)Events;

            return null;
        }
    }
}
