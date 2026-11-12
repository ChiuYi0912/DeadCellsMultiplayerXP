using DeadCellsMultiplayerX.Contracts.Entities;
using MessagePack;


namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// 通用实体移除事件
    /// </summary>
    [MessagePackObject]
    public class EntityDespawn
    {
        /// <summary>
        /// 网络实体 id
        /// </summary>
        [Key(0)] public uint NetId { get; set; }

        /// <summary>
        /// 实体类型
        /// </summary>
        [Key(1)] public EntityKind Kind { get; set; }

        /// <summary>
        /// 移除原因
        /// </summary>
        [Key(2)] public EntityDespawnReason Reason { get; set; }
    }
}
