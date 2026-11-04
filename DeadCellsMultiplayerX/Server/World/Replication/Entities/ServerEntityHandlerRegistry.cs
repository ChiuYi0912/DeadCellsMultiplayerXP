using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data.Entities;

namespace DeadCellsMultiplayerX.Server.World.Replication.Entities
{
    /// <summary>
    /// 服务端实体类型处理器注册表
    /// </summary>
    internal sealed class ServerEntityHandlerRegistry
    {
        private readonly Dictionary<EntityKind, IServerEntityKindHandler> handlers = [];

        public void Register(IServerEntityKindHandler handler)
        {
            if (handler == null) return;
            handlers[handler.Kind] = handler;
        }

        public bool TryGet(EntityKind kind, out IServerEntityKindHandler handler)
            => handlers.TryGetValue(kind, out handler!);
    }
}
