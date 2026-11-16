namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 客户端上报的盾牌相位；格挡/弹反结果由服务端判定。
/// </summary>
public enum ShieldIntentKind : byte
{
    HoldStart = 0,
    HoldEnd = 1,
    ParryStart = 2,
    ParryEnd = 3,
}
