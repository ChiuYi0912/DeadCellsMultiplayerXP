namespace DeadCellsMultiplayerX.Server.Interactions;

/// <summary>
/// 交互裁决与拒绝原因。集中定义，避免魔法字符串。
/// </summary>
internal static class InteractionReasons
{
    public const string Rejected = "rejected";

    public const string RoundBusy = "round busy";

    public const string AlreadyRecorded = "already recorded";

    public const string Unresolvable = "unresolvable";

    public const string NotParticipant = "not a participant";

    public const string NoParticipants = "no participants";

    public const string NoVotes = "no votes";

    public const string MissingConnectionId = "missing connection id";

    public const string MissingPlayerId = "missing player id";

    public const string PlayerIdMismatch = "player id mismatch";
}
