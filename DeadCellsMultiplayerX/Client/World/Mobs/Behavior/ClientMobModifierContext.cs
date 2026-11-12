using dc.en;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Heroes.Replay;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
namespace DeadCellsMultiplayerX.Client.World.Mobs.Behavior
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
            ClientReplayProxyManager RemoteGhost)
        {
            Mob = mob;
            NetId = netId;
            Tick = tick;
            Session = session;
            this.RemoteGhost = RemoteGhost;
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

        public ClientReplayProxyManager RemoteGhost { get; }

        /// <inheritdoc />
        public T GetService<T>() where T : class
        {
            if (typeof(T) == typeof(GuestClientSession))
                return (T)(object)Session;

            if (typeof(T) == typeof(ClientReplayProxyManager))
                return (T)(object)RemoteGhost;

            return null!;
        }
    }
}
