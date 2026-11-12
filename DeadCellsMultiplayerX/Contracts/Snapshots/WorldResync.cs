using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// 全量世界基线,用于历史帧已被淘汰或首次同步
    /// </summary>
    [MessagePackObject]
    public class WorldResync
    {
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// 基线序号,客户端 next = Sequence + 1
        /// </summary>
        [Key(1)] public long Sequence { get; set; }
        [Key(2)] public double ServerTimeMs { get; set; }
        [Key(3)] public List<HeroDynamic> Heroes { get; set; } = [];
        [Key(4)] public List<HeroSpawn> Spawns { get; set; } = [];

        /// <summary>
        /// 全量实体动态状态
        /// </summary>
        [Key(5)] public List<EntityDynamic> Entities { get; set; } = [];

        /// <summary>
        /// 全量实体生成数据
        /// </summary>
        [Key(6)] public List<EntitySpawn> EntitySpawns { get; set; } = [];

        /// <summary>
        /// 全量动态关卡区域
        /// </summary>
        [Key(7)] public List<LevelAreaAffectState> AreaAffects { get; set; } = [];
    }
}
