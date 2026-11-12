namespace DeadCellsMultiplayerX.Server.Replication.Entities;

using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 一次发布产生的通用实体数据
/// </summary>
internal sealed class EntityPublishResult
{
    /// <summary>
    /// 新生成实体
    /// </summary>
    public List<EntitySpawn> Spawned { get; } = [];

    /// <summary>
    /// 当前存活实体状态
    /// </summary>
    public List<EntityDynamic> Entities { get; } = [];

    /// <summary>
    /// 本帧移除的实体
    /// </summary>
    public List<EntityDespawn> Despawned { get; } = [];
}
