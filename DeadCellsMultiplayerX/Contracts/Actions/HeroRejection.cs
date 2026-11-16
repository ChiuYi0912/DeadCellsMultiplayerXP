namespace DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 动作被拒绝的稳定原因。
/// </summary>
public enum HeroRejection
{
    None = 0,
    InvalidRequest = 1,
    EpochMismatch = 2,
    InvalidSlot = 3,
    InvalidRatio = 4,
    InvalidCommandId = 5,
    GameUnavailable = 6,
    NoClientState = 7,
    HeroDead = 8,
    LevelMismatch = 9,
    LevelUnavailable = 10,
    GhostUnavailable = 11,
    UnsupportedKind = 12,
    LoadoutUnavailable = 13,
    NotReady = 14,
    ExecutionFailed = 15,
    ExecutionException = 16,
}
