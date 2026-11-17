namespace DeadCellsMultiplayerX.Client.World.Entities;

using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// Indexes authoritative spawn records and current native instances.
/// ClientEntityReplication owns creation, retry, repair and destruction.
/// </summary>
internal sealed class ClientEntityStore
{
    private readonly Dictionary<uint, ClientEntityRecord> byId = [];
    private readonly Dictionary<object, uint> byNative = [with(ReferenceEqualityComparer.Instance)];

    public IEnumerable<ClientEntityRecord> Records => this.byId.Values;
    public ClientEntityRecord? GetRecord(uint netId) => this.byId.GetValueOrDefault(netId);
    public object? Get(uint netId) => this.GetRecord(netId)?.Native;

    public bool TryGetNetId(object native, out uint netId)
    {
        if (native != null && this.byNative.TryGetValue(native, out netId))
        {
            return true;
        }
        netId = 0;
        return false;
    }

    public bool IsReplicated(object native) => native != null && this.byNative.ContainsKey(native);

    internal ClientEntityRecord Track(EntitySpawn spawn)
    {
        if (!this.byId.TryGetValue(spawn.NetId, out var record))
        {
            record = new ClientEntityRecord { Spawn = spawn };
            this.byId.Add(spawn.NetId, record);
        }
        return record;
    }

    internal bool Contains(ClientEntityRecord record)
        => ReferenceEquals(this.GetRecord(record.NetId), record);

    internal bool TryBind(ClientEntityRecord record, object native)
    {
        if (!this.Contains(record) || record.Native != null || this.byNative.ContainsKey(native))
        {
            return false;
        }
        record.Native = native;
        this.byNative.Add(native, record.NetId);
        return true;
    }

    internal object? Unbind(ClientEntityRecord record)
    {
        var native = record.Native;
        if (native != null)
        {
            this.byNative.Remove(native);
            record.Native = null;
        }
        return native;
    }

    internal ClientEntityRecord? Remove(uint netId)
    {
        if (!this.byId.Remove(netId, out var record))
        {
            return null;
        }
        // Retain Native until the lifetime owner releases this detached record.
        if (record.Native != null)
        {
            this.byNative.Remove(record.Native);
        }
        return record;
    }
}
