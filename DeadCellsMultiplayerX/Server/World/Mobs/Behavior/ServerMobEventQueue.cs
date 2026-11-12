using DeadCellsMultiplayerX.Contracts.MobBehavior;
namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior
{
    /// <summary>
    /// 服务端 mob 出站事件队列实现。
    /// </summary>
    internal sealed class ServerMobEventQueue : IServerMobEventQueue
    {
        private readonly List<MobModifierEvent> pending = [];

        /// <inheritdoc />
        public void Enqueue(MobModifierEvent evt)
        {
            if (evt == null)
                return;

            pending.Add(evt);
        }

        /// <inheritdoc />
        public IReadOnlyList<MobModifierEvent> Drain()
        {
            if (pending.Count == 0)
                return [];

            var result = pending.ToArray();
            pending.Clear();
            return result;
        }

        /// <inheritdoc />
        public void Clear()
        {
            pending.Clear();
        }
    }
}
