using System.Collections.Generic;
using dc;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Client.Replication.Entities;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.EntityEvents;

namespace DeadCellsMultiplayerX.Client.UI.DebugTools
{
    internal sealed class EntityDebugLabelManager
    {
        private const long UpdateIntervalMs = 150;

        private readonly Dictionary<EntityKind, IEntityDebugTextProvider> providers = [];
        private readonly Dictionary<uint, Entry> active = [];

        private long lastUpdateMs;
        private bool visible;

        private sealed class Entry
        {
            public required object Native { get; init; }
            public required IEntityDebugTextProvider Provider { get; init; }
            public string Text = string.Empty;
        }

        public void Register(IEntityDebugTextProvider provider)
        {
            if (provider == null)
                return;

            providers[provider.Kind] = provider;
        }

        /// <summary>
        /// 实体创建后开始跟踪
        /// </summary>
        public void OnCreated(ClientEntityRecord record)
        {
            if (record?.Native == null)
                return;

            if (!providers.TryGetValue(record.Kind, out var provider))
                return;

            active[record.NetId] = new Entry
            {
                Native = record.Native,
                Provider = provider,
            };
        }

        public void OnRemoved(ClientEntityRecord record)
        {
            if (record == null)
                return;

            active.Remove(record.NetId);
        }

        /// <summary>
        /// 实体事件按类型分发
        /// </summary>
        public void OnEntityEvent(EntityEvent evt)
        {
            if (evt == null)
                return;

            if (providers.TryGetValue(evt.EntityKind, out var provider))
                provider.OnEvent(evt);
        }

        public void Reset()
        {
            active.Clear();

            foreach (var provider in providers.Values)
                provider.Reset();
        }

        public void Tick(ClientEntityContext ctx, long nowMs)
        {
            bool show = DebugFlags.Get("entityLabels");
            if (show != visible)
            {
                visible = show;
                if (!show)
                    ClearLabels();
            }

            if (!show)
                return;

            if (nowMs - lastUpdateMs < UpdateIntervalMs)
                return;

            lastUpdateMs = nowMs;

            List<uint>? stale = null;

            foreach (var (netId, entry) in active)
            {
                if (entry.Native is not Entity entity || entity.destroyed)
                {
                    (stale ??= []).Add(netId);
                    continue;
                }

                string? text = entry.Provider.GetText(
                    netId, entry.Native, ctx, nowMs);

                if (string.IsNullOrEmpty(text))
                {
                    if (entry.Text.Length == 0)
                        continue;

                    ClearLabel(entity, entry.Provider);
                    entry.Text = string.Empty;
                    continue;
                }

                if (entry.Text == text)
                    continue;

                entity.setGameplayLabel(
                    text.AsHaxeString(),
                    Ref<int>.In(entry.Provider.LabelColor));
                entry.Text = text;
            }

            if (stale == null)
                return;

            foreach (var netId in stale)
                active.Remove(netId);
        }

        private void ClearLabels()
        {
            foreach (var entry in active.Values)
            {
                if (entry.Native is not Entity entity || entity.destroyed)
                    continue;

                if (entry.Text.Length == 0)
                    continue;

                ClearLabel(entity, entry.Provider);
                entry.Text = string.Empty;
            }
        }

        private static void ClearLabel(
            Entity entity, IEntityDebugTextProvider provider)
            => entity.setGameplayLabel(null, Ref<int>.In(provider.LabelColor));
    }
}
