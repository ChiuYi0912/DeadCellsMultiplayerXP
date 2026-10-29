using System;
using System.Collections.Generic;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Server.World.Ghosts.Execution;
using DeadCellsMultiplayerX.Server.World.Ghosts.World;
using DeadCellsMultiplayerX.Server.World.Hubs;
using Serilog;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.Pipeline
{
    /// <summary>
    /// Ghost 动作管线
    /// </summary>
    internal sealed class ActionPipeline
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        private readonly IReadOnlyList<IActionStage> stages;
        private readonly Func<long> clock;

        private ActionPipeline(
            IReadOnlyList<IActionStage> stages,
            Func<long> clock)
        {
            this.stages = stages;
            this.clock = clock;
        }

        /// <summary>
        /// 组装服务端默认管线
        /// </summary>
        public static ActionPipeline CreateDefault(
            WorldState world,
            SpawnSynchronizer spawns,
            ClientWorldFrameCollector frames,
            ServerMobRegistry registry,
            ServerMobHistory history,
            Func<long> clock,
            Func<Game?> game)
        {
            var executor = ActionExecutor.CreateDefault();
            var rewind = new MobRewind(registry, history);

            IActionStage[] stages =
            [
                new AdmissionStage(world, frames, game),
                new BindingStage(world, spawns),
                new ExecutionStage(executor, rewind),
                new PublicationStage(world),
            ];

            return new ActionPipeline(stages, clock);
        }

        /// <summary>
        /// 处理一个客户端动作
        /// </summary>
        public void Handle(string senderGuid, HeroAction? request)
        {
            var context = new ActionContext(
                senderGuid, request, clock());

            foreach (var stage in stages)
            {
                var result = stage.Process(context);
                if (!result.IsRejected) continue;

                LogRejection(context, result.Rejection);
                return;
            }
        }

        private static void LogRejection(
            ActionContext context,
            Rejection rejection)
        {
            if (rejection is Rejection.None
                or Rejection.ExecutionRejected)
            {
                return;
            }

            var request = context.Request;

            if (rejection == Rejection.TooFar)
            {
                Logger.Warning(
                    "Reject action: guid={Guid} kind={Kind} slot={Slot} distance too far",
                    context.SenderGuid, request?.Kind, request?.Slot);
                return;
            }

            Logger.Debug(
                "Reject action: guid={Guid} kind={Kind} slot={Slot} reason={Reason}",
                context.SenderGuid, request?.Kind, request?.Slot, rejection);
        }
    }
}
