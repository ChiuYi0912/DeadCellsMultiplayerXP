namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.EntityEvents;
using MessagePack;

/// <summary>
/// 服务端每 tick 广播的完整世界帧
/// </summary>
[MessagePackObject]
public class WorldFrame
{
    /// <summary>
    /// 世界重置代号
    /// </summary>
    [Key(0)] public int Epoch { get; set; }

    /// <summary>
    /// 广播序号,epoch 内从 0 连续
    /// </summary>
    [Key(1)] public long Sequence { get; set; }

    /// <summary>
    /// 服务端时间戳(毫秒)
    /// </summary>
    [Key(2)] public double ServerTimeMs { get; set; }

    /// <summary>
    /// 全量英雄动态状态
    /// </summary>
    [Key(3)] public List<HeroDynamic> Heroes { get; set; } = [];

    /// <summary>
    /// 新增/静态变化的英雄
    /// </summary>
    [Key(4)] public List<HeroSpawn> Spawned { get; set; } = [];

    /// <summary>
    /// 本帧移除的 GUID
    /// </summary>
    [Key(5)] public List<string> Despawned { get; set; } = [];

    /// <summary>
    /// 全量实体动态状态
    /// </summary>
    [Key(6)] public List<EntityDynamic> Entities { get; set; } = [];

    /// <summary>
    /// 新生成实体的静态数据
    /// </summary>
    [Key(7)] public List<EntitySpawn> SpawnedEntities { get; set; } = [];

    /// <summary>
    /// 本帧移除的实体
    /// </summary>
    [Key(8)] public List<EntityDespawn> DespawnedEntities { get; set; } = [];

    /// <summary>
    /// 本帧服务端确认的英雄动作（主动技能/武器）
    /// </summary>
    [Key(9)] public List<HeroActionEvent> Actions { get; set; } = [];

    /// <summary>
    /// 服务端权威的动态关卡区域状态
    /// </summary>
    [Key(10)] public List<LevelAreaAffectState> AreaAffects { get; set; } = [];

    /// <summary>
    /// 本帧统一路由的实体事件
    /// </summary>
    [Key(11)] public List<EntityEvent> EntityEvents { get; set; } = [];
}
