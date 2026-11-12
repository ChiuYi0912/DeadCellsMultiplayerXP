namespace DeadCellsMultiplayerX.Client.World.Entities;

using dc;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 客户端通用实体存储
/// </summary>
internal sealed class ClientEntityStore
{
    private readonly Dictionary<uint, ClientEntityRecord> byId = [];
    private readonly Dictionary<object, uint> byNative =
        [with(ReferenceEqualityComparer.Instance)];
    private readonly List<IClientEntityKindHandler> handlers = [];
    private readonly Dictionary<EntityKind, IClientEntityKindHandler> byKind = [];

    public IReadOnlyList<IClientEntityKindHandler> Handlers => this.handlers;

    /// <summary>
    /// 原生实体创建完成
    /// </summary>
    public event Action<ClientEntityRecord>? RecordCreated;

    /// <summary>
    /// 实体记录被移除
    /// </summary>
    public event Action<ClientEntityRecord>? RecordRemoved;

    public IEnumerable<ClientEntityRecord> Records => this.byId.Values;

    public void RegisterHandler(IClientEntityKindHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        this.byKind[handler.Kind] = handler;
        if (!this.handlers.Contains(handler))
        {
            this.handlers.Add(handler);
        }
    }

    public bool TryGetHandler(EntityKind kind, out IClientEntityKindHandler handler)
        => this.byKind.TryGetValue(kind, out handler!);

    public ClientEntityRecord? GetRecord(uint netId)
        => this.byId.TryGetValue(netId, out var record) ? record : null;

    public object? Get(uint netId)
        => this.byId.TryGetValue(netId, out var record) ? record.Native : null;

    public bool TryGetNetId(object native, out uint netId)
    {
        if (native != null && this.byNative.TryGetValue(native, out netId))
        {
            return true;
        }

        netId = 0;
        return false;
    }

    public bool IsReplicated(object native)
        => native != null && this.byNative.ContainsKey(native);

    public bool HasOrPending(uint netId) => this.byId.ContainsKey(netId);

    public bool IsMissingOrDestroyed(uint netId)
    {
        if (!this.byId.TryGetValue(netId, out var record))
        {
            return false;
        }

        if (record.Native == null)
        {
            return true;
        }

        return record.Native is Entity entity && entity.destroyed;
    }

    public void OnSpawned(EntitySpawn spawn)
    {
        if (spawn == null || spawn.NetId == 0)
        {
            return;
        }

        if (this.byId.TryGetValue(spawn.NetId, out var existing))
        {
            this.TryCreateRecord(existing);
            return;
        }

        var record = new ClientEntityRecord { Spawn = spawn };
        this.byId[spawn.NetId] = record;
        this.TryCreateRecord(record);
    }

    public void RetryPending()
    {
        if (this.byId.Count == 0)
        {
            return;
        }

        List<ClientEntityRecord>? pending = null;
        foreach (var record in this.byId.Values)
        {
            if (record.Native == null)
            {
                (pending ??= []).Add(record);
            }
        }

        if (pending == null)
        {
            return;
        }

        foreach (var record in pending)
        {
            this.TryCreateRecord(record);
        }
    }

    private bool TryCreateRecord(ClientEntityRecord record)
    {
        if (record.Native is Entity { destroyed: true })
        {
            this.byNative.Remove(record.Native);
            record.Native = null;
        }

        if (record.Native != null)
        {
            return true;
        }

        if (!this.byKind.TryGetValue(record.Kind, out var handler))
        {
            return false;
        }

        if (!handler.TryCreate(record.Spawn, out var native)
            || native == null)
        {
            return false;
        }

        record.Native = native;
        this.byNative[native] = record.NetId;
        handler.OnCreated(record.Spawn, native);
        RecordCreated?.Invoke(record);
        return true;
    }

    /// <summary>
    /// 尝试重建已销毁/未创建的实体 用于服务端修复
    /// </summary>
    public bool TryRecreate(uint netId)
    {
        if (!this.byId.TryGetValue(netId, out var record))
        {
            return false;
        }

        if (record.Native is Entity entity && !entity.destroyed)
        {
            return false;
        }

        if (record.Native != null)
        {
            if (this.byKind.TryGetValue(record.Kind, out var handler))
            {
                handler.Despawn(
                    record.NetId, record.Native,
                    EntityDespawnReason.Removed);
            }

            RecordRemoved?.Invoke(record);
            this.byNative.Remove(record.Native);
            record.Native = null;
        }

        return this.TryCreateRecord(record);
    }

    public void Remove(uint netId, EntityDespawnReason reason)
    {
        if (!this.byId.Remove(netId, out var record))
        {
            return;
        }

        if (record.Native != null)
        {
            this.byNative.Remove(record.Native);
        }

        if (this.byKind.TryGetValue(record.Kind, out var handler))
        {
            handler.Despawn(netId, record.Native, reason);
        }

        RecordRemoved?.Invoke(record);
    }

    /// <summary>
    /// 以服务端全量集合为准清理多余实体
    /// pending 记录不参与清理
    /// </summary>
    public void Reconcile(IReadOnlyList<EntityDynamic> entities)
    {
        if (entities == null)
        {
            return;
        }

        HashSet<uint> authoritative = [];
        foreach (var entity in entities)
        {
            if (entity != null)
            {
                authoritative.Add(entity.NetId);
            }
        }

        List<uint>? remove = null;
        foreach (var record in this.byId.Values)
        {
            if (record.Native == null)
            {
                continue;
            }

            if (authoritative.Contains(record.NetId))
            {
                continue;
            }

            (remove ??= []).Add(record.NetId);
        }

        if (remove == null)
        {
            return;
        }

        foreach (var netId in remove)
        {
            this.Remove(netId, EntityDespawnReason.Reconciled);
        }
    }

    /// <summary>
    /// 清空全部记录；所有移除都经过本类的 <see cref="Remove"/>，
    /// 确保 handler 执行原生实体销毁。
    /// </summary>
    public void Clear()
    {
        var netIds = new List<uint>(this.byId.Keys);
        foreach (var netId in netIds)
        {
            this.Remove(netId, EntityDespawnReason.LevelUnloaded);
        }

        this.byId.Clear();
        this.byNative.Clear();
    }
}
