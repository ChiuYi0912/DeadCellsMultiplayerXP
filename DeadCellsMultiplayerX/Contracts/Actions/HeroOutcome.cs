namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 服务端对一次动作的权威结果。
/// 物品、tiers、pose 与盾牌结果均以服务端状态为准。
/// </summary>
[MessagePackObject]
public class HeroOutcome
{
    [Key(0)] public int Epoch { get; set; }

    /// <summary>
    /// 服务端每 epoch 单调递增；客户端按 (CasterGuid, OutcomeId) 去重。
    /// </summary>
    [Key(1)] public long OutcomeId { get; set; }

    /// <summary>
    /// 对应 HeroCommand.CommandId；世界事件为 0。
    /// </summary>
    [Key(2)] public long CommandId { get; set; }

    [Key(3)] public HeroOutcomeCause Cause { get; set; }

    [Key(4)] public string CasterGuid { get; set; } = string.Empty;

    [Key(5)] public HeroActionKind Kind { get; set; }

    [Key(6)] public int Slot { get; set; }

    /// <summary>
    /// 服务端从 Loadout 解析出的物品。
    /// </summary>
    [Key(7)] public ItemRef Item { get; set; } = new();

    [Key(8)] public int CX { get; set; }
    [Key(9)] public int CY { get; set; }
    [Key(10)] public double XR { get; set; }
    [Key(11)] public double YR { get; set; }
    [Key(12)] public int Dir { get; set; }
    [Key(13)] public double DX { get; set; }
    [Key(14)] public double DY { get; set; }

    [Key(15)] public double Ratio { get; set; } = 1.0;
    [Key(16)] public int Cycle { get; set; }

    [Key(17)] public int BrutalityTier { get; set; }
    [Key(18)] public int SurvivalTier { get; set; }
    [Key(19)] public int TacticTier { get; set; }

    [Key(20)] public string LevelId { get; set; } = string.Empty;
    [Key(21)] public int SubLevelIndex { get; set; } = -1;
    [Key(22)] public long ServerTimeMs { get; set; }

    [Key(23)] public HeroOutcomeStatus Status { get; set; }

    [Key(24)] public HeroRejection Rejection { get; set; }

    /// <summary>
    /// 盾牌结果；非盾牌动作为 null。
    /// </summary>
    [Key(25)] public ShieldOutcome? Shield { get; set; }
}
