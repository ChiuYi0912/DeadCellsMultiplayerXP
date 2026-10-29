using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Contracts;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Execution;
using Serilog;

namespace DeadCellsMultiplayerX.Server.MobAI.Combat
{
    /// <summary>
    /// 攻击释放路由骨架
    /// 当前只负责收集计划并产生事件；具体技能执行留给未来的 IMobExecutor
    /// </summary>
    internal sealed class ServerMobAttackRouter : IServerMobAttackRouter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerMobAttackRouter");

        private readonly IMobExecutor? executor;
        private readonly IServerMobEventBus? events;
        private readonly Queue<MobActionPlan> pending = new();

        public ServerMobAttackRouter(
            IMobExecutor? executor = null,
            IServerMobEventBus? events = null)
        {
            this.executor = executor;
            this.events = events;
        }

        /// <inheritdoc />
        public void Submit(MobActionPlan plan)
        {
            if (plan == null || plan.ActionId <= 0 || plan.MobNetId == 0)
                return;

            pending.Enqueue(plan);
        }

        /// <inheritdoc />
        public IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs)
        {
            if (pending.Count == 0)
                return [];

            var result = new List<MobModifierEvent>();

            while (pending.TryDequeue(out var plan))
            {
                bool started = executor?.TryStart(plan) ?? true;
                if (!started)
                {
                    Logger.Debug(
                        "Mob attack rejected by executor: mob={NetId} action={ActionId} skill={Skill}",
                        plan.MobNetId, plan.ActionId, plan.SkillId);
                    continue;
                }

                var evt = new MobModifierEvent
                {
                    Epoch = plan.Epoch,
                    ActionId = plan.ActionId,
                    MobNetId = plan.MobNetId,
                    Kind = MobModifierEventKind.ActionStarted,
                    SkillId = plan.SkillId,
                    TargetGuid = plan.TargetGuid,
                    ServerTimeMs = serverTimeMs,
                    LevelId = plan.LevelId,
                    SubLevelIndex = plan.SubLevelIndex,
                };

                result.Add(evt);
                events?.Publish(evt);
            }

            return result;
        }

        /// <inheritdoc />
        public void Clear() => pending.Clear();
    }
}
