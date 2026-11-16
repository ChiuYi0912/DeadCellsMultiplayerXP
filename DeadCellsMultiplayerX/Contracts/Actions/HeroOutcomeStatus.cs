namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 一次动作处理的终态。
/// </summary>
public enum HeroOutcomeStatus
{
    Executed = 0,
    Rejected = 1,
    Duplicate = 2,
    Expired = 3,
}
