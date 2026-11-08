using MessagePack;
using DeadCellsMultiplayerX.Common.EntityEvents;

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
    /// mob 修改器产生的纯数据事件
    /// 公共字段位于 EntityEvent 基类
    /// </summary>
    [MessagePackObject]
    public sealed class MobModifierEvent : EntityEvent
    {
        /// <summary>
        /// 动作 id
        /// </summary>
        [Key(10)] public long ActionId { get; set; }

        /// <summary>
        /// 事件类型
        /// </summary>
        [Key(11)] public MobModifierEventKind Kind { get; set; }

        /// <summary>
        /// 技能 id；非技能事件可为空
        /// </summary>
        [Key(12)] public string SkillId { get; set; } = string.Empty;

        /// <summary>
        /// 目标玩家 guid；无目标时为空
        /// </summary>
        [Key(13)] public string TargetGuid { get; set; } = string.Empty;

        /// <summary>
        /// 预留的自定义负载
        /// </summary>
        [Key(14)] public string? Data { get; set; }

        [Key(15)] public double? SkillRatio { get; set; }
    }
}
