using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Server.MobAI.Events;

namespace DeadCellsMultiplayerX.Server.MobAI.Combat
{
    /// <summary>
    /// 技能事件路由
    /// </summary>
    internal sealed class ServerMobAttackRouter : IServerMobAttackRouter
    {
        private readonly IServerMobEventBus? events;
        private readonly Queue<MobModifierEvent> pendingSkills = new();
        private long nextActionId;

        public ServerMobAttackRouter(
            IServerMobEventBus? events = null)
        {
            this.events = events;
        }

        /// <inheritdoc />
        public void SubmitSkill(MobModifierEvent evt)
        {
            if (evt == null || evt.MobNetId == 0)
                return;

            pendingSkills.Enqueue(evt);
        }

        /// <inheritdoc />
        public IReadOnlyList<MobModifierEvent> Tick(long serverTimeMs)
        {
            if (pendingSkills.Count == 0)
                return [];

            var result = new List<MobModifierEvent>();

            while (pendingSkills.TryDequeue(out var evt))
            {
                evt.ActionId = ++nextActionId;
                if (evt.ServerTimeMs <= 0)
                    evt.ServerTimeMs = serverTimeMs;

                result.Add(evt);
                events?.Publish(evt);
            }

            return result;
        }

        /// <inheritdoc />
        public void Clear()
        {
            pendingSkills.Clear();
            nextActionId = 0;
        }
    }
}
