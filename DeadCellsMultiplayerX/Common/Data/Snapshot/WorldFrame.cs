using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 服务端每 tick 广播的完整世界帧
    /// 怪物在此扩展
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
        /// 全量怪物动态状态
        /// </summary>
        [Key(6)] public List<MobDynamic> Mobs { get; set; } = [];

        /// <summary>
        /// 新生成怪物的实体数据
        /// </summary>
        [Key(7)] public List<MobSpawn> SpawnedMobs { get; set; } = [];

        /// <summary>
        /// 本帧移除的怪物 NetId
        /// </summary>
        [Key(8)] public List<uint> DespawnedMobs { get; set; } = [];

        /// <summary>
        /// 本帧服务端确认的英雄动作（主动技能/武器）
        /// </summary>
        [Key(9)] public List<HeroActionEvent> Actions { get; set; } = [];

        /// <summary>
        /// 服务端权威的动态关卡区域状态
        /// </summary>
        [Key(10)] public List<LevelAreaAffectState> AreaAffects { get; set; } = [];

        /// <summary>
        /// 本帧服务端判定的 mob 命中 Ghost 事件
        /// </summary>
        [Key(11)] public List<GhostHitEvent> GhostHits { get; set; } = [];
    }
}
