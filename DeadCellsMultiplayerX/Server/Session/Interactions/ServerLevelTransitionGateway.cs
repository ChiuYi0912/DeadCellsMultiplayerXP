namespace DeadCellsMultiplayerX.Server.Session.Interactions;

using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Server.Interactions;
using DeadCellsMultiplayerX.Server.World;
using Microsoft.VisualStudio.Threading;
using Serilog;


internal sealed class ServerLevelTransitionGateway(ServerSession session, WorldCoordinator worlds, ILogger logger)
{
    private readonly ServerSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly LevelTransitionInteraction transition = new(worlds.EnterNextLevelAsync);

    public ValueTask<InteractionDispatch> GusetEnterNextLevelAsync(
        string connectionGuid,
        string? payloadPlayerId,
        string? levelId)
    {
        if (!TryResolveActor(
                connectionGuid,
                payloadPlayerId,
                requirePayloadIdentity: true,
                out var reason))
        {
            this.logger.Warning("Level transition rejected: {Reason}", reason);
            return new ValueTask<InteractionDispatch>(InteractionDispatch.Rejected(reason));
        }

        return this.SubmitAsync(connectionGuid, levelId);
    }


    public ValueTask<InteractionDispatch> SubmitLevelVoteAsync(
        string connectionGuid,
        LevelVote vote)
    {
        if (!TryResolveActor(
                connectionGuid,
                vote.PlyerID,
                requirePayloadIdentity: false,
                out var reason))
        {
            this.logger.Warning("Level transition rejected: {Reason}", reason);
            return new ValueTask<InteractionDispatch>(InteractionDispatch.Rejected(reason));
        }

        return this.SubmitAsync(connectionGuid, vote.NextLevelID);
    }


    /// <summary>
    /// 调试，强制服务端切换关卡并广播
    /// </summary>
    public async ValueTask ForceLevelAsync(
        string levelId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(levelId);
        await this.session.GameContext;
        this.session.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.session.IsDisposed, this.session);
        this.logger.Warning("Forcing level transition: {Level}", levelId);
        await this.transition.ForceAsync(levelId, cancellationToken);
    }

    private async ValueTask<InteractionDispatch> SubmitAsync(
        string actor,
        string? levelId)
    {
        await this.session.GameContext;
        this.session.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.session.IsDisposed, this.session);
        var dispatch = await this.transition.SubmitAsync(
            actor,
            levelId ?? string.Empty,
            this.Participants(),
            CancellationToken.None);

        this.LogDispatch(dispatch);
        return dispatch;
    }

    private string[] Participants() => [.. this.session.ConnectedGuestIds];

    private void LogDispatch(InteractionDispatch dispatch)
    {
        switch (dispatch.Status)
        {
            case InteractionDispatchStatus.Resolved:
                this.logger.Information("Level transition committed and published.");
                break;
            case InteractionDispatchStatus.Duplicate:
                this.logger.Debug("Level transition duplicate ignored: {Reason}", dispatch.Reason);
                break;
            case InteractionDispatchStatus.Rejected:
                this.logger.Warning("Level transition rejected: {Reason}", dispatch.Reason);
                break;
            case InteractionDispatchStatus.Aborted:
                this.logger.Warning("Level transition aborted: {Reason}", dispatch.Reason);
                break;
            case InteractionDispatchStatus.Accepted:
                break;
            default:
                break;
        }
    }

    private static bool TryResolveActor(
        string? connectionGuid,
        string? payloadPlayerId,
        bool requirePayloadIdentity,
        out string reason)
    {
        if (string.IsNullOrWhiteSpace(connectionGuid))
        {
            reason = InteractionReasons.MissingConnectionId;
            return false;
        }

        if (requirePayloadIdentity && string.IsNullOrWhiteSpace(payloadPlayerId))
        {
            reason = InteractionReasons.MissingPlayerId;
            return false;
        }

        if (!string.IsNullOrWhiteSpace(payloadPlayerId)
            && !string.Equals(payloadPlayerId, connectionGuid, StringComparison.Ordinal))
        {
            reason = InteractionReasons.PlayerIdMismatch;
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
