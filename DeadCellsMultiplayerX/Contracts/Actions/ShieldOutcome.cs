namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 服务端盾牌结果；命令相位与原生格挡事件共用。
/// BlockKind != None 表示这是一次真实格挡。
/// </summary>
[MessagePackObject]
public class ShieldOutcome
{
    /// <summary>
    /// 命令相位；BlockKind != None 时忽略。
    /// </summary>
    [Key(0)] public ShieldIntentKind Phase { get; set; }

    [Key(1)] public ShieldBlockKind BlockKind { get; set; }

    /// <summary>
    /// 是否来自按住格挡（Partial），而不是弹反窗口（Full）。
    /// </summary>
    [Key(2)] public bool Held { get; set; }

    [Key(3)] public bool Countered { get; set; }

    /// <summary>
    /// 触发格挡的 mob NetId；0 表示无直接来源。
    /// </summary>
    [Key(4)] public uint SourceNetId { get; set; }

    /// <summary>
    /// 引擎 applyStunAndBumpFromParry 使用的眩晕时长。
    /// </summary>
    [Key(5)] public long StunDurationMs { get; set; }

    /// <summary>
    /// 服务端是否执行了 Mob.interruptSkills()。
    /// </summary>
    [Key(6)] public bool SkillInterrupted { get; set; }

    /// <summary>
    /// 服务端弹反窗口结束时间，供表现层对齐。
    /// </summary>
    [Key(7)] public long ParryWindowEndMs { get; set; }
}
