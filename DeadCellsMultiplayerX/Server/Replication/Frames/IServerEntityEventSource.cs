namespace DeadCellsMultiplayerX.Server.Replication.Frames;

using DeadCellsMultiplayerX.Contracts.EntityEvents;

/// <summary>
/// 服务端实体事件来源
/// 每种实体管理器各自实现并注册到广播器
/// </summary>
internal interface IServerEntityEventSource
{
    public IReadOnlyList<EntityEvent> DrainEntityEvents();
}
