using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.EntityEvents;

namespace DeadCellsMultiplayerX.Client.Replication.Entities
{
    /// <summary>
    /// 客户端实体类型的创建与状态应用处理器
    /// </summary>
    internal interface IClientEntityKindHandler
    {
        EntityKind Kind { get; }

        /// <summary>
        /// 创建原生实体。返回 false 表示当前还不能创建 保持 pending
        /// </summary>
        bool TryCreate(
            EntitySpawn spawn, ClientEntityContext ctx, out object native);

        /// <summary>
        /// 创建Mob之后运行 用于修改对应实体的初始化字段
        /// </summary>
        void OnCreated(
            EntitySpawn spawn, object native, ClientEntityContext ctx);

        /// <summary>
        /// 实体移除
        /// </summary>
        void Despawn(
            uint netId, object? native,
            EntityDespawnReason reason, ClientEntityContext ctx);

        /// <summary>
        /// 处理本帧 spawn 之前调用
        /// </summary>
        void BeforeSpawns(WorldFrame frame, ClientEntityContext ctx) { }

        /// <summary>
        /// 处理完本帧 spawn 之后调用
        /// </summary>
        void AfterSpawns(WorldFrame frame, ClientEntityContext ctx) { }

        /// <summary>
        /// 对账与移除完成后调用
        /// </summary>
        void AfterFrame(WorldFrame frame, ClientEntityContext ctx) { }

        /// <summary>
        /// 每帧状态推进
        /// </summary>
        void Tick(ClientEntityContext ctx, long nowMs) { }

        /// <summary>
        /// 该实体类型的事件 由通用实体帧管线按 EntityKind 分发
        /// </summary>
        void OnEntityEvent(EntityEvent evt, ClientEntityContext ctx) { }

        /// <summary>
        /// epoch 变化时的清理
        /// </summary>
        void OnEpochReset(ClientEntityContext ctx) { }
    }
}
