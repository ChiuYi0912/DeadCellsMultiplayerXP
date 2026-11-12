using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Server.World.Mobs.Behavior;
namespace DeadCellsMultiplayerX.Server.World.Mobs.Combat
{
    /// <summary>
    /// 技能事件路由
    /// </summary>
    internal sealed class ServerMobAttackRouter : IServerMobAttackRouter
    {
        private readonly IServerMobEventQueue? events;
        private readonly Queue<MobModifierEvent> pendingSkills = new();
        private long nextActionId;

        public ServerMobAttackRouter(
            IServerMobEventQueue? events = null)
        {
            this.events = events;
        }

        /// <inheritdoc />
        public void SubmitSkill(MobModifierEvent evt)
        {
            if (evt == null || evt.NetId == 0)
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
                events?.Enqueue(evt);
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
