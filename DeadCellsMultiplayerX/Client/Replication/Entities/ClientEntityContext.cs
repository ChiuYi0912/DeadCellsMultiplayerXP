using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Replication.Entities
{
    /// <summary>
    /// 客户端实体处理上下文
    /// </summary>
    internal sealed class ClientEntityContext
    {
        public ClientEntityContext(
            GuestClientSession session,
            RemoteWorld world,
            ClientEntityStore store)
        {
            Session = session;
            World = world;
            Store = store;
        }

        public GuestClientSession Session { get; }

        public RemoteWorld World { get; }

        public ClientEntityStore Store { get; }
    }
}
