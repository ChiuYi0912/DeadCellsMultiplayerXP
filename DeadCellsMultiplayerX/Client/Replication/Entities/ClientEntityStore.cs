using System;
using System.Collections.Generic;
using dc;
using dc.en;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Client.Replication.Entities
{
    /// <summary>
    /// 客户端通用实体存储
    /// </summary>
    internal sealed class ClientEntityStore
    {
        private readonly Dictionary<uint, ClientEntityRecord> byId = [];
        private readonly Dictionary<object, uint> byNative =
            new(ReferenceEqualityComparer.Instance);
        private readonly List<IClientEntityKindHandler> handlers = [];
        private readonly Dictionary<EntityKind, IClientEntityKindHandler> byKind = [];

        public ClientEntityStore(GuestClientSession session, RemoteWorld world)
        {
            Context = new ClientEntityContext(session, world, this);
        }

        public ClientEntityContext Context { get; }

        public IReadOnlyList<IClientEntityKindHandler> Handlers => handlers;

        /// <summary>
        /// 原生实体创建完成
        /// </summary>
        public event Action<ClientEntityRecord>? RecordCreated;

        /// <summary>
        /// 实体记录被移除
        /// </summary>
        public event Action<ClientEntityRecord>? RecordRemoved;

        public IEnumerable<ClientEntityRecord> Records => byId.Values;

        public void RegisterHandler(IClientEntityKindHandler handler)
        {
            if (handler == null) return;

            byKind[handler.Kind] = handler;
            if (!handlers.Contains(handler))
                handlers.Add(handler);
        }

        public bool TryGetHandler(EntityKind kind, out IClientEntityKindHandler handler)
            => byKind.TryGetValue(kind, out handler!);

        public ClientEntityRecord? GetRecord(uint netId)
            => byId.TryGetValue(netId, out var record) ? record : null;

        public object? Get(uint netId)
            => byId.TryGetValue(netId, out var record) ? record.Native : null;

        public bool TryGetNetId(object native, out uint netId)
        {
            if (native != null && byNative.TryGetValue(native, out netId))
                return true;

            netId = 0;
            return false;
        }

        public bool IsReplicated(object native)
            => native != null && byNative.ContainsKey(native);

        public bool HasOrPending(uint netId) => byId.ContainsKey(netId);

        public bool IsMissingOrDestroyed(uint netId)
        {
            if (!byId.TryGetValue(netId, out var record))
                return false;

            if (record.Native == null)
                return true;

            return record.Native is Entity entity && entity.destroyed;
        }

        public void OnSpawned(EntitySpawn spawn)
        {
            if (spawn == null || spawn.NetId == 0)
                return;

            if (byId.TryGetValue(spawn.NetId, out var existing))
            {
                TryCreateRecord(existing);
                return;
            }

            var record = new ClientEntityRecord { Spawn = spawn };
            byId[spawn.NetId] = record;
            TryCreateRecord(record);
        }

        public void RetryPending()
        {
            if (byId.Count == 0) return;

            List<ClientEntityRecord>? pending = null;
            foreach (var record in byId.Values)
            {
                if (record.Native == null)
                    (pending ??= []).Add(record);
            }

            if (pending == null) return;

            foreach (var record in pending)
                TryCreateRecord(record);
        }

        private bool TryCreateRecord(ClientEntityRecord record)
        {
            if (record.Native is Entity { destroyed: true })
            {
                byNative.Remove(record.Native);
                record.Native = null;
            }

            if (record.Native != null) return true;
            if (!byKind.TryGetValue(record.Kind, out var handler)) return false;

            if (!handler.TryCreate(record.Spawn, Context, out var native)
                || native == null)
                return false;

            record.Native = native;
            byNative[native] = record.NetId;
            handler.OnCreated(record.Spawn, native, Context);
            RecordCreated?.Invoke(record);
            return true;
        }

        /// <summary>
        /// 尝试重建已销毁/未创建的实体 用于服务端修复
        /// </summary>
        public bool TryRecreate(uint netId)
        {
            if (!byId.TryGetValue(netId, out var record))
                return false;

            if (record.Native is Entity entity && !entity.destroyed)
                return false;

            if (record.Native != null)
            {
                if (byKind.TryGetValue(record.Kind, out var handler))
                {
                    handler.Despawn(
                        record.NetId, record.Native,
                        EntityDespawnReason.Removed, Context);
                }

                RecordRemoved?.Invoke(record);
                byNative.Remove(record.Native);
                record.Native = null;
            }

            return TryCreateRecord(record);
        }

        public void Remove(uint netId, EntityDespawnReason reason)
        {
            if (!byId.Remove(netId, out var record))
                return;

            if (record.Native != null)
                byNative.Remove(record.Native);

            if (byKind.TryGetValue(record.Kind, out var handler))
                handler.Despawn(netId, record.Native, reason, Context);

            RecordRemoved?.Invoke(record);
        }

        /// <summary>
        /// 以服务端全量集合为准清理多余实体
        /// pending 记录不参与清理
        /// </summary>
        public void Reconcile(IReadOnlyList<EntityDynamic> entities)
        {
            if (entities == null)
                return;

            HashSet<uint> authoritative = [];
            foreach (var entity in entities)
            {
                if (entity != null)
                    authoritative.Add(entity.NetId);
            }

            List<uint>? remove = null;
            foreach (var record in byId.Values)
            {
                if (record.Native == null) continue;
                if (authoritative.Contains(record.NetId)) continue;
                (remove ??= []).Add(record.NetId);
            }

            if (remove == null) return;

            foreach (var netId in remove)
                Remove(netId, EntityDespawnReason.Reconciled);
        }

        /// <summary>
        /// 清空映射
        /// </summary>
        public void Clear()
        {
            byId.Clear();
            byNative.Clear();
        }
    }
}
