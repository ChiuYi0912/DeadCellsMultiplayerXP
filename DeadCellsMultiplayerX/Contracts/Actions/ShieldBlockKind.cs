namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 服务端从原生 HitResult 推导出的格挡类型。
/// </summary>
public enum ShieldBlockKind : byte
{
    None = 0,
    Partial = 1,
    Full = 2,
}
