using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.EntityEvents;

namespace DeadCellsMultiplayerX.Server.World.Replication.Frames
{
    /// <summary>
    /// 服务端实体事件来源
    /// 每种实体管理器各自实现并注册到广播器
    /// </summary>
    internal interface IServerEntityEventSource
    {
        IReadOnlyList<EntityEvent> DrainEntityEvents();
    }
}
