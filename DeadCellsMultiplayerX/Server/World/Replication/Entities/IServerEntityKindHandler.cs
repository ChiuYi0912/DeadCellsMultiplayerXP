using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Server.World.Replication.Entities
{
    /// <summary>
    /// 服务端实体类型的采集处理器
    /// 新增实体种类时只需实现此接口并注册，不需要改帧组装
    /// </summary>
    internal interface IServerEntityKindHandler
    {
        EntityKind Kind { get; }

        /// <summary>
        /// 原生实体是否仍然存活并应参与同步
        /// </summary>
        bool IsAlive(ServerEntityEntry entry);

        /// <summary>
        /// 序列化静态生成数据
        /// 返回 false 表示本帧还不能发布
        /// </summary>
        bool TryPrepareSpawn(ServerEntityEntry entry);

        /// <summary>
        /// 构建每帧动态状态；null 表示本帧跳过
        /// </summary>
        EntityDynamic? BuildDynamic(ServerEntityEntry entry, long serverTimeMs);
    }
}
