using System;
using System.Collections.Generic;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Diagnostics;
using DeadCellsMultiplayerX.Server.World.Ghosts.Execution;
using DeadCellsMultiplayerX.Server.World.Ghosts.World;
using DeadCellsMultiplayerX.Server.World.Replication.Frames;
using DeadCellsMultiplayerX.Server.World.Replication.Entities;
using Serilog;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.Pipeline
{
    /// <summary>
    /// Ghost 动作管线。
    /// 思路：Admission -> Binding -> Execution -> Publication 四阶段顺序执行，
    /// 任一阶段拒绝即停止；可选 ActionJournal 记录接受/拒绝原因，便于定位“动作为什么丢”。
    /// </summary>
    internal sealed class ActionPipeline
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostManager");

        private readonly IReadOnlyList<IActionStage> stages;
        private readonly Func<long> clock;
        private readonly ActionJournal? journal;

        private ActionPipeline(
            IReadOnlyList<IActionStage> stages,
            Func<long> clock,
            ActionJournal? journal)
        {
            this.stages = stages;
            this.clock = clock;
            this.journal = journal;
        }

        /// <summary>
        /// 组装服务端默认管线
        /// </summary>
        public static ActionPipeline CreateDefault(
            WorldState world,
            SpawnSynchronizer spawns,
            ClientWorldFrameCollector frames,
            ServerEntityRegistry registry,
            ServerEntityHistory history,
            Func<long> clock,
            Func<Game?> game,
            ActionJournal? journal = null)
        {
            var executor = ActionExecutor.CreateDefault(registry, clock);
            var rewind = new MobRewind(registry, history);

            IActionStage[] stages =
            [
                new AdmissionStage(world, frames, game),
                new BindingStage(world, spawns),
                new ExecutionStage(executor, rewind),
                new PublicationStage(world),
            ];

            return new ActionPipeline(stages, clock, journal);
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
                Record(senderGuid, request, context, false, result.Rejection);
                return;
            }

            Record(senderGuid, request, context, true, Rejection.None);
        }

        private void Record(
            string senderGuid,
            HeroAction? request,
            ActionContext context,
            bool accepted,
            Rejection rejection)
        {
            journal?.Record(
                senderGuid,
                request?.Kind.ToString() ?? "null",
                request?.Slot ?? -1,
                accepted,
                rejection.ToString(),
                request?.ClientTimeMs ?? 0,
                context.ServerTimeMs,
                request?.LevelId ?? string.Empty);
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
