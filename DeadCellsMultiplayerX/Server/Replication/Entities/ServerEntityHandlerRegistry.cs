namespace DeadCellsMultiplayerX.Server.Replication.Entities;

using DeadCellsMultiplayerX.Contracts.Entities;

/// <summary>
/// 服务端实体类型处理器注册表
/// </summary>
internal sealed class ServerEntityHandlerRegistry
{
    private readonly Dictionary<EntityKind, IServerEntityKindHandler> handlers = [];

    public void Register(IServerEntityKindHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        this.handlers[handler.Kind] = handler;
    }

    public bool TryGet(EntityKind kind, out IServerEntityKindHandler handler)
        => this.handlers.TryGetValue(kind, out handler!);
}
