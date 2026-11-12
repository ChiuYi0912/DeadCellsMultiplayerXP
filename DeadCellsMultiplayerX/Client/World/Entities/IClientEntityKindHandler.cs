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
    /// 创建Mob之后运行 用于修改对应实体的初始化字段
    /// </summary>
    public void OnCreated(EntitySpawn spawn, object native);

    /// <summary>
    /// 实体移除
    /// </summary>
    public void Despawn(
        uint netId, object? native, EntityDespawnReason reason);

    /// <summary>
    /// 处理本帧 spawn 之前调用
    /// </summary>
    public void BeforeSpawns(WorldFrame frame) { }

    /// <summary>
    /// 处理完本帧 spawn 之后调用
    /// </summary>
    public void AfterSpawns(WorldFrame frame) { }

    /// <summary>
    /// 对账与移除完成后调用
    /// </summary>
    public void AfterFrame(WorldFrame frame) { }

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
