using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Contracts;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Events;
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

        private readonly IServerMobEventBus? events;
        private readonly Queue<MobModifierEvent> pendingHits = new();
        private long nextActionId;

        public ServerMobAttackRouter(
            IServerMobEventBus? events = null)
        {
            this.events = events;
        }

        /// <inheritdoc />
        public void SubmitHit(MobModifierEvent hit)
        {
            if (hit == null || hit.MobNetId == 0)
                return;

            pendingHits.Enqueue(hit);
        }

        /// <inheritdoc />
        public IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs)
        {
            if (pendingHits.Count == 0)
                return [];

            var result = new List<MobModifierEvent>();

            while (pendingHits.TryDequeue(out var hit))
            {
                hit.ActionId = ++nextActionId;
                hit.Kind = MobModifierEventKind.AttackHit;
                if (hit.ServerTimeMs <= 0)
                    hit.ServerTimeMs = serverTimeMs;

                result.Add(hit);
                events?.Publish(hit);
            }

            return result;
        }

        /// <inheritdoc />
        public void Clear()
        {
            pendingHits.Clear();
            nextActionId = 0;
        }
    }
}
