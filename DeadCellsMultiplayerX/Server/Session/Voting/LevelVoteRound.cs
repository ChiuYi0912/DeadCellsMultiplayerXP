namespace DeadCellsMultiplayerX.Server.Session.Voting;

/// <summary>One epoch's electorate and choices. No network or native operations.</summary>
internal sealed class LevelVoteRound(int epoch, IEnumerable<string> participants, Func<int, int>? nextIndex = null)
{
    private readonly HashSet<string> participants = new(participants, StringComparer.Ordinal);
    private readonly Dictionary<string, string> votes = [with(StringComparer.Ordinal)];
    private readonly Func<int, int> nextIndex = nextIndex ?? Random.Shared.Next;

    public int Epoch { get; } = epoch;
    public bool IsEmpty => this.participants.Count == 0;

    public bool TryRecord(string actor, string levelId, out string? reason)
    {
        if (!this.participants.Contains(actor))
        {
            reason = "not a participant in this round";
        }
        else if (!this.votes.TryAdd(actor, levelId))
        {
            reason = "vote already recorded";
        }
        else
        {
            reason = null;
            return true;
        }
        return false;
    }

    public void Remove(string actor)
    {
        this.participants.Remove(actor);
        this.votes.Remove(actor);
    }

    public bool TryChoose(out string levelId)
    {
        levelId = string.Empty;
        if (this.IsEmpty || this.votes.Count != this.participants.Count)
        {
            return false;
        }
        // Preserve the existing rule: pick among distinct destinations, not weighted votes.
        var choices = this.votes.Values.Distinct(StringComparer.Ordinal).ToArray();
        levelId = choices[choices.Length == 1 ? 0 : this.nextIndex(choices.Length)];
        return true;
    }
}
