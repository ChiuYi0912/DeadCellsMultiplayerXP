using MessagePack;
using PolyType;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.MobAI.Events;

namespace DeadCellsMultiplayerX.Common.EntityEvents
{
    [MessagePackObject]
    [Union(0, typeof(MobModifierEvent))]
    [DerivedTypeShape(typeof(MobModifierEvent), Tag = 0)]
    public abstract class EntityEvent
    {
        /// <summary>
        /// 世界重置代号
        /// </summary>
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// 事件所属实体的网络 id
        /// </summary>
        [Key(1)] public uint NetId { get; set; }

        /// <summary>
        /// 实体类型，客户端据此分发
        /// </summary>
        [Key(2)] public EntityKind EntityKind { get; set; }

        /// <summary>
        /// 服务端时间戳
        /// </summary>
        [Key(3)] public long ServerTimeMs { get; set; }

        /// <summary>
        /// 事件发生时所在关卡
        /// </summary>
        [Key(4)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时所在子关卡
        /// </summary>
        [Key(5)] public int SubLevelIndex { get; set; } = -1;
    }
}
