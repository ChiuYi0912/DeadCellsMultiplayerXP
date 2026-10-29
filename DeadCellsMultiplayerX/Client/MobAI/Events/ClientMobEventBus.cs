using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using Serilog;

namespace DeadCellsMultiplayerX.Client.MobAI.Events
{
    /// <summary>
    /// 客户端 mob 事件总线实现
    /// </summary>
    internal sealed class ClientMobEventBus : IClientMobEventBus
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientMobEventBus");

        private readonly List<MobModifierEvent> pending = [];
        private readonly List<Action<MobModifierEvent>> subscribers = [];

        /// <inheritdoc />
        public void Publish(MobModifierEvent evt)
        {
            if (evt == null)
                return;

            pending.Add(evt);

            foreach (var handler in subscribers.ToArray())
            {
                handler(evt);
            }
        }

        /// <inheritdoc />
        public void Subscribe(Action<MobModifierEvent> handler)
        {
            if (handler != null)
                subscribers.Add(handler);
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
            subscribers.Clear();
        }
    }
}
