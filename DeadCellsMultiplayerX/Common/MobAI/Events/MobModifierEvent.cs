using DeadCellsMultiplayerX.Common.Data.Entities;
using MessagePack;

namespace DeadCellsMultiplayerX.Common.MobAI.Events
{
    /// <summary>
    /// 修改器事件类型
    /// </summary>
    public enum MobModifierEventKind
    {
        /// <summary>
        /// 动作开始
        /// </summary>
        ActionStarted = 0,

        /// <summary>
        /// 动作阶段变化
        /// </summary>
        ActionPhase = 1,

        /// <summary>
        /// 动作结束
        /// </summary>
        ActionEnded = 2,

        /// <summary>
        /// 攻击命中
        /// </summary>
        AttackHit = 3,

        /// <summary>
        /// mob 死亡
        /// </summary>
        MobDied = 4,

        /// <summary>
        /// 自定义事件
        /// </summary>
        Custom = 5,
    }

    /// <summary>
    /// 修改器产生的纯数据事件
    /// 同时用于服务端/客户端进程内总线与 WorldFrame 传输。
    /// </summary>
    [MessagePackObject]
    public sealed class MobModifierEvent
    {
        /// <summary>
        /// 世界重置代号
        /// </summary>
        [Key(0)] public int Epoch { get; set; }

        /// <summary>
        /// 动作 id
        /// </summary>
        [Key(1)] public long ActionId { get; set; }

        /// <summary>
        /// 产生事件的 mob
        /// </summary>
        [Key(2)] public uint MobNetId { get; set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        [Key(3)] public MobModifierEventKind Kind { get; set; }

        /// <summary>
        /// 技能 id；非技能事件可为空
        /// </summary>
        [Key(4)] public string SkillId { get; set; } = string.Empty;

        /// <summary>
        /// 目标玩家 guid；无目标时为空
        /// </summary>
        [Key(5)] public string TargetGuid { get; set; } = string.Empty;

        /// <summary>
        /// 服务端时间戳
        /// </summary>
        [Key(6)] public long ServerTimeMs { get; set; }

        /// <summary>
        /// 事件发生时所在关卡
        /// </summary>
        [Key(7)] public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 事件发生时所在子关卡
        /// </summary>
        [Key(8)] public int SubLevelIndex { get; set; } = -1;

        /// <summary>
        /// 事件所属实体类型
        /// </summary>
        [Key(9)] public EntityKind EntityKind { get; set; } = EntityKind.Mob;

        /// <summary>
        /// 预留的自定义负载
        /// </summary>
        [Key(10)] public string? Data { get; set; }

        [Key(11)] public double? SkillRatio { get; set; }
    }
}
