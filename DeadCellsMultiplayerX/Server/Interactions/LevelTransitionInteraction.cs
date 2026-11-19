namespace DeadCellsMultiplayerX.Server.Interactions;

internal sealed class LevelTransitionInteraction(
    Func<string, CancellationToken, ValueTask> serverEnterLevel,
    Func<int, int>? nextIndex = null)
    : ServerInteraction<string, string>
{
    private readonly Func<string, CancellationToken, ValueTask> serverEnterLevel = serverEnterLevel
            ?? throw new ArgumentNullException(nameof(serverEnterLevel));

    private readonly Func<int, int> nextIndex = nextIndex ?? Random.Shared.Next;

    protected override bool TryAdmit(
        string actor,
        string levelId,
        IReadOnlyCollection<string> participants,
        out string? reason)
    {
        if (!participants.Contains(actor, StringComparer.Ordinal))
        {
            reason = InteractionReasons.NotParticipant;
            return false;
        }

        reason = null;
        return true;
    }

    protected override ResolutionStatus TryResolve(
        IReadOnlyCollection<string> participants,
        out string decision,
        out string? reason)
    {
        decision = string.Empty;

        if (participants.Count == 0)
        {
            reason = InteractionReasons.NoParticipants;
            return ResolutionStatus.Unresolvable;
        }

        foreach (var participant in participants)
        {
            if (!this.Intents.ContainsKey(participant))
            {
                reason = null;
                return ResolutionStatus.Pending;
            }
        }

        var candidates = this.Intents.Values.Distinct().ToList();
        if (candidates.Count == 0)
        {
            reason = InteractionReasons.NoVotes;
            return ResolutionStatus.Unresolvable;
        }

        var index = candidates.Count == 1
            ? 0
            : this.nextIndex(candidates.Count);

        decision = candidates[index];
        reason = null;
        return ResolutionStatus.Resolved;
    }

    protected override ValueTask CommitAsync(string levelId, CancellationToken cancellationToken)
        => this.serverEnterLevel(levelId, cancellationToken);

    protected override ValueTask PublishAsync(string levelId, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}
