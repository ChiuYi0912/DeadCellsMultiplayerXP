namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 服务端确认并广播的一次英雄动作
/// </summary>
[MessagePackObject]
public class HeroActionEvent
{
    [Key(0)] public int Epoch { get; set; }

    /// <summary>
    /// 与 HeroAction.ActionId 一致 用于客户端去重
    /// </summary>
    [Key(1)] public long ActionId { get; set; }

    [Key(2)] public string CasterGuid { get; set; } = string.Empty;

    [Key(3)] public HeroActionKind Kind { get; set; }

    [Key(4)] public int Slot { get; set; }

    [Key(5)] public ItemRef Item { get; set; } = new();

    [Key(6)] public int CX { get; set; }
    [Key(7)] public int CY { get; set; }
    [Key(8)] public double XR { get; set; }
    [Key(9)] public double YR { get; set; }
    [Key(10)] public int Dir { get; set; }
    [Key(11)] public double DX { get; set; }
    [Key(12)] public double DY { get; set; }

    [Key(13)] public double Ratio { get; set; } = 1.0;
    [Key(14)] public int Cycle { get; set; }

    [Key(15)] public int BrutalityTier { get; set; }
    [Key(16)] public int SurvivalTier { get; set; }
    [Key(17)] public int TacticTier { get; set; }

    [Key(18)] public string LevelId { get; set; } = string.Empty;
    [Key(19)] public int SubLevelIndex { get; set; } = -1;
    [Key(20)] public long ServerTimeMs { get; set; }

    [Key(21)] public ShieldActionKind ShieldKind { get; set; }
    [Key(22)] public bool FullParry { get; set; }
    [Key(23)] public uint SourceNetId { get; set; }
    [Key(24)] public long SourceActionId { get; set; }
}
