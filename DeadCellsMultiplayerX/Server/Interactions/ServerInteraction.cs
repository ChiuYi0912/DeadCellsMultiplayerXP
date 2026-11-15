namespace DeadCellsMultiplayerX.Server.Interactions;

internal enum InteractionStatus
{
    Idle,
    Collecting,
    Resolving,
}

internal enum InteractionDispatchStatus
{
    Accepted,
    Duplicate,
    Rejected,
    Resolved,
    Aborted,
}

internal readonly record struct InteractionDispatch(InteractionDispatchStatus Status, string? Reason = null)
{
    public static InteractionDispatch Accepted { get; } = new(InteractionDispatchStatus.Accepted);

    public static InteractionDispatch Resolved { get; } = new(InteractionDispatchStatus.Resolved);

    public static InteractionDispatch Duplicate(string reason)
        => new(InteractionDispatchStatus.Duplicate, reason);

    public static InteractionDispatch Rejected(string reason)
        => new(InteractionDispatchStatus.Rejected, reason);

    public static InteractionDispatch Aborted(string reason)
        => new(InteractionDispatchStatus.Aborted, reason);
}

internal enum ResolutionStatus
{
    Pending,
    Resolved,
    Unresolvable,
}


internal abstract class ServerInteraction<TIntent, TDecision>
    where TIntent : notnull
{
    private readonly Dictionary<string, TIntent> intents = [with(StringComparer.Ordinal)];

    public bool HasActiveRound => this.Status != InteractionStatus.Idle;

    public InteractionStatus Status { get; private set; }

    protected IReadOnlyDictionary<string, TIntent> Intents => this.intents;


    public async ValueTask<InteractionDispatch> SubmitAsync(
        string actor,
        TIntent intent,
        IReadOnlyCollection<string> participants,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(participants);

        if (!this.TryAdmit(actor, intent, participants, out var admitReason))
        {
            return InteractionDispatch.Rejected(admitReason ?? InteractionReasons.Rejected);
        }

        if (this.Status == InteractionStatus.Idle)
        {
            this.intents.Clear();
            this.Status = InteractionStatus.Collecting;
        }
        else if (this.Status != InteractionStatus.Collecting)
        {
            return InteractionDispatch.Rejected(InteractionReasons.RoundBusy);
        }

        if (this.intents.ContainsKey(actor))
        {
            return InteractionDispatch.Duplicate(InteractionReasons.AlreadyRecorded);
        }

        this.intents[actor] = intent;

        var resolution = this.TryResolve(participants, out var decision, out var reason);
        if (resolution == ResolutionStatus.Pending)
        {
            return InteractionDispatch.Accepted;
        }

        if (resolution == ResolutionStatus.Unresolvable)
        {
            var abortReason = reason ?? InteractionReasons.Unresolvable;
            this.intents.Clear();
            this.Status = InteractionStatus.Idle;
            return InteractionDispatch.Aborted(abortReason);
        }

        this.Status = InteractionStatus.Resolving;

        try
        {
            await this.CommitAsync(decision, cancellationToken);
            await this.PublishAsync(decision, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            this.intents.Clear();
            this.Status = InteractionStatus.Idle;
            throw;
        }
        catch (Exception ex)
        {
            this.intents.Clear();
            this.Status = InteractionStatus.Idle;
            return InteractionDispatch.Aborted(ex.Message);
        }

        this.intents.Clear();
        this.Status = InteractionStatus.Idle;
        return InteractionDispatch.Resolved;
    }

    /// <summary>
    /// 准入校验
    /// </summary>
    protected abstract bool TryAdmit(
        string actor,
        TIntent intent,
        IReadOnlyCollection<string> participants,
        out string? reason);

    /// <summary>
    /// 判断本轮是否可裁决
    /// </summary>
    protected abstract ResolutionStatus TryResolve(
        IReadOnlyCollection<string> participants,
        out TDecision decision,
        out string? reason);

    /// <summary>
    /// 应用到服务端游戏状态
    /// </summary>
    protected abstract ValueTask CommitAsync(
        TDecision decision,
        CancellationToken cancellationToken);

    /// <summary>
    /// 广播给客户端
    /// </summary>
    protected abstract ValueTask PublishAsync(
        TDecision decision,
        CancellationToken cancellationToken);
}
