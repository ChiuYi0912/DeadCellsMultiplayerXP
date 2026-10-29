using System.Collections.Generic;
using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 客户端请求服务端补发广播帧
    /// </summary>
    [MessagePackObject]
    public class WorldFrameRangeRequest
    {
        [Key(0)] public long FromSequence { get; set; }
        [Key(1)] public long ToSequence { get; set; }
    }

    /// <summary>
    /// 服务端请求客户端补发上传帧
    /// </summary>
    [MessagePackObject]
    public class ClientFrameRangeRequest
    {
        [Key(0)] public long FromSequence { get; set; }
        [Key(1)] public long ToSequence { get; set; }
    }

    /// <summary>
    /// 服务端对补发请求的响应
    /// </summary>
    [MessagePackObject]
    public class WorldFrameBundle
    {
        [Key(0)] public List<WorldFrame> Frames { get; set; } = [];
        [Key(1)] public WorldResync? Resync { get; set; }
    }

    /// <summary>
    /// 客户端对补发请求的响应
    /// </summary>
    [MessagePackObject]
    public class ClientFrameBundle
    {
        [Key(0)] public List<ClientWorldFrame> Frames { get; set; } = [];
        [Key(1)] public bool Unavailable { get; set; }
    }

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
        /// 全量怪物动态状态
        /// </summary>
        [Key(5)] public List<MobDynamic> Mobs { get; set; } = [];

        /// <summary>
        /// 全量怪物生成数据
        /// </summary>
        [Key(6)] public List<MobSpawn> MobSpawns { get; set; } = [];

        /// <summary>
        /// 全量动态关卡区域
        /// </summary>
        [Key(7)] public List<LevelAreaAffectState> AreaAffects { get; set; } = [];
    }
}
