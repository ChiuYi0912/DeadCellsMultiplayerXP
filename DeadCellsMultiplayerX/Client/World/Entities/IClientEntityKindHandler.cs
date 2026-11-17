namespace DeadCellsMultiplayerX.Client.World.Entities;

using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 客户端实体类型的创建与状态应用处理器
/// </summary>
internal interface IClientEntityKindHandler
{
    public EntityKind Kind { get; }

    /// <summary>
    /// 创建原生实体。返回 false 表示当前还不能创建 保持 pending
    /// </summary>
    public bool TryCreate(EntitySpawn spawn, out object native);

    /// <summary>
    /// 原生实体已经绑定到存储后初始化。失败时由管线撤销绑定并调用 Despawn。
    /// </summary>
    public void OnCreated(EntitySpawn spawn, object native);

    /// <summary>
    /// 实体移除
    /// </summary>
    public void Despawn(
        uint netId, object? native, EntityDespawnReason reason);

    /// <summary>
    /// 生成后、显式移除及对账前处理权威状态。
    /// 需要修复时同步调用 tryRecreate；原生实例替换仍由实体管线执行。
    /// 不得保存或异步使用本帧的重建入口。
    /// </summary>
    public void ApplyFrame(WorldFrame frame, Func<uint, bool> tryRecreate) { }

    /// <summary>
    /// 每帧状态推进
    /// </summary>
    public void Tick(long nowMs) { }

    /// <summary>
    /// 该实体类型的事件 由通用实体帧管线按 EntityKind 分发
    /// </summary>
    public void OnEntityEvent(EntityEvent evt) { }

    /// <summary>
    /// epoch 变化时的清理
    /// </summary>
    public void OnEpochReset() { }
}
