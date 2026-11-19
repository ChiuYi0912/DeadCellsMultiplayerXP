namespace DeadCellsMultiplayerX.Server.World.Replication.Frames;

using DeadCellsMultiplayerX.Contracts.EntityEvents;

/// <summary>
/// 服务端实体事件来源
/// 每种实体管理器各自实现并注册到组帧器
/// </summary>
internal interface IServerEntityEventSource
{
    public IReadOnlyList<EntityEvent> PeekEntityEvents();

    public void CommitEntityEvents(int count);
}
