namespace DeadCellsMultiplayerX.Server.Session.Voting;

using DeadCellsMultiplayerX.Contracts.Levels;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Server.World;
using Microsoft.VisualStudio.Threading;
using Serilog;

/// <summary>Validates connection votes and dispatches one transition per world.</summary>
internal sealed class ServerLevelTransitionGateway(ServerSession session, WorldCoordinator worlds, ILogger logger)
{
    private LevelVoteRound? round;
    private bool committing;

    public async Task SubmitLevelVoteAsync(string connectionGuid, LevelVote? vote)
    {
        await session.GameContext;
        session.VerifyGameThread();
        ObjectDisposedException.ThrowIf(session.IsDisposed, session);
        if (vote == null || string.IsNullOrWhiteSpace(vote.NextLevelID)
            || (!string.IsNullOrEmpty(vote.PlyerID) && vote.PlyerID != connectionGuid))
        {
            logger.Warning("Invalid level vote from {Guid}", connectionGuid);
            return;
        }
        if (this.committing || worlds.CurrentWorld is not { IsRunning: true } world
            || vote.Epoch != world.Epoch)
        {
            logger.Debug("Ignoring level vote: guid={Guid} epoch={Epoch}, current={Current}",
                connectionGuid, vote.Epoch, worlds.Epoch);
            return;
        }
        var participants = session.ConnectedGuests.Where(guest => !guest.IsDisposed)
            .Select(guest => guest.ConnectionGuid).ToArray();
        if (!participants.Contains(connectionGuid, StringComparer.Ordinal))
        {
            return;
        }

        this.round ??= new(world.Epoch, participants);
        if (this.round.Epoch != vote.Epoch)
        {

            throw new InvalidOperationException("Vote round outlived its world.");
        }

        if (!this.round.TryRecord(connectionGuid, vote.NextLevelID, out var reason))
        {
            logger.Debug("Level vote ignored for {Guid}: {Reason}", connectionGuid, reason);
            return;
        }
        if (this.round.TryChoose(out var levelId))
        {
            await this.CommitAsync(levelId, session.DisposeToken);
        }
    }

    public void OnGuestRemoved(string guid)
    {
        session.VerifyGameThread();
        if (session.IsDisposed || this.committing || this.round == null)
        {
            return;
        }

        this.round.Remove(guid);
        if (this.round.IsEmpty)
        {
            this.round = null;
        }
        else if (this.round.TryChoose(out var levelId))
        {
            _ = this.ObserveDisconnectTransitionAsync(this.CommitAsync(levelId, session.DisposeToken));
        }

    }

    public async ValueTask ForceLevelAsync(string levelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(levelId);
        await session.GameContext;
        session.VerifyGameThread();
        ObjectDisposedException.ThrowIf(session.IsDisposed, session);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.committing || this.round != null)
        {

            throw new InvalidOperationException("Cannot force a level while voting or transitioning.");
        }

        await this.CommitAsync(levelId, cancellationToken);
    }

    private async Task CommitAsync(string levelId, CancellationToken cancellationToken)
    {
        this.committing = true;
        try
        {
            await worlds.EnterNextLevelAsync(levelId, cancellationToken);
        }
        finally
        {
            this.round = null;
            this.committing = false;
        }
    }

    // Connection removal is synchronous; the coordinator owns and joins the actual world operation.
    private async Task ObserveDisconnectTransitionAsync(Task transition)
    {
        try
        { await transition; }
        catch (OperationCanceledException) when (session.IsDisposed) { }
        catch (Exception ex) { logger.Error(ex, "Level transition after disconnect failed"); }
    }
}
