namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 动作意图的通用参数。
/// 物品身份、tiers、pose 由服务端从权威状态解析；盾牌结果由服务端原生判定。
/// </summary>
[MessagePackObject]
public class HeroIntent
{
    [Key(0)] public double Ratio { get; set; } = 1.0;

    [Key(1)] public int Cycle { get; set; }

    [Key(2)] public ShieldIntentKind ShieldKind { get; set; }
}
