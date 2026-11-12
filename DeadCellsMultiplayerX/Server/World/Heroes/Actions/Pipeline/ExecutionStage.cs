namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

internal sealed class ExecutionStage(
    ActionExecutor executor,
    MobRewind rewind) : IActionStage
{
    private readonly ActionExecutor executor = executor;
    private readonly MobRewind rewind = rewind;

    public StageResult Process(ActionContext context)
    {
        var request = context.Request!;

        IDisposable? scope = request.Kind == HeroActionKind.Weapon
            ? this.rewind.Begin(
                context.Level!,
                request.ClientTimeMs,
                context.ServerTimeMs)
            : null;

        try
        {
            return this.executor.TryExecute(
                    context.Ghost!, request, context.Ratio)
                ? StageResult.Continue
                : StageResult.Reject(Rejection.ExecutionRejected);
        }
        finally
        {
            scope?.Dispose();
        }
    }
}

/// <summary>
/// 构建并记录广播事件
/// </summary>

