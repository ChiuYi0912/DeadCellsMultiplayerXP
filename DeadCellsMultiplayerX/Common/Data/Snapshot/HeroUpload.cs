using System.Collections.Generic;
using MessagePack;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Data.Snapshot
{
    /// <summary>
    /// 客户端每帧上传给服务端的统一包
    /// </summary>
    [MessagePackObject]
    public class HeroUpload
    {
        /// <summary>
        /// 客户端本地时间
        /// </summary>
        [Key(0)] public long ClientTime { get; set; }

        /// <summary>
        /// 客户端帧号
        /// </summary>
        [Key(1)] public uint FrameId { get; set; }

        /// <summary>
        /// 静态数据
        /// </summary>
        [Key(2)] public HeroSpawn? Spawn { get; set; }

        /// <summary>
        /// 动态数据
        /// </summary>
        [Key(3)] public HeroDynamic Dynamic { get; set; } = null!;
    }
}