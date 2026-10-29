using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using Serilog;

namespace DeadCellsMultiplayerX.Server.MobAI.Events
{
    /// <summary>
    /// 服务端 mob 事件总线实现。
    /// </summary>
    internal sealed class ServerMobEventBus : IServerMobEventBus
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerMobEventBus");

        private readonly List<MobModifierEvent> pending = [];
        private readonly List<Action<MobModifierEvent>> subscribers = [];

        /// <inheritdoc />
        public void Publish(MobModifierEvent evt)
        {
            if (evt == null)
                return;

            pending.Add(evt);

            if (subscribers.Count == 0)
                return;

            foreach (var handler in subscribers.ToArray())
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex,
                        "Mob event subscriber failed: kind={Kind} mob={NetId}",
                        evt.Kind, evt.MobNetId);
                }
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
