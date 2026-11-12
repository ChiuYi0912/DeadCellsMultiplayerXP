using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline
{
    internal sealed class ExecutionStage : IActionStage
    {
        private readonly ActionExecutor executor;
        private readonly MobRewind rewind;

        public ExecutionStage(
            ActionExecutor executor,
            MobRewind rewind)
        {
            this.executor = executor;
            this.rewind = rewind;
        }

        public StageResult Process(ActionContext context)
        {
            var request = context.Request!;

            IDisposable? scope = request.Kind == HeroActionKind.Weapon
                ? rewind.Begin(
                    context.Level!,
                    request.ClientTimeMs,
                    context.ServerTimeMs)
                : null;

            try
            {
                return executor.TryExecute(
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
}
