namespace DeadCellsMultiplayerX.Client.World.Entities;

using dc;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// Owns entity lifetimes: spawn, retry, native replacement, reconciliation and cleanup.
/// Kind handlers supply native behavior; the store only maintains indexes.
/// </summary>
internal sealed class ClientEntityFrameApplier(ClientEntityStore store) : IDisposable
{
    private readonly Dictionary<EntityKind, IClientEntityKindHandler> handlers = [];
    private int lastEpoch = -1;
    private int generation;
    private bool disposed;
    private bool clearing;

    public ClientEntityStore Store { get; } = store;
    public EntityDebugLabelManager DebugLabels { get; } = new();

    public void RegisterHandler(IClientEntityKindHandler handler)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        ArgumentNullException.ThrowIfNull(handler);
        this.handlers.Add(handler.Kind, handler);
    }

    public void ApplyFrame(WorldFrame frame)
    {
        if (this.disposed || this.clearing || frame == null)
        {
            return;
        }
        if (frame.Epoch != this.lastEpoch)
        {
            this.ClearCore();
            if (this.disposed)
            {
                return;
            }
            this.lastEpoch = frame.Epoch;
        }
        var current = this.generation;
        if (frame.SpawnedEntities != null)
        {
            foreach (var spawn in frame.SpawnedEntities)
            {
                if (!this.IsActive(current))
                {
                    return;
                }
                if (spawn != null && spawn.NetId != 0)
                {
                    this.TryCreate(this.Store.Track(spawn));
                }
            }
        }
        foreach (var handler in this.handlers.Values)
        {
            if (!this.IsActive(current))
            {
                return;
            }
            handler.ApplyFrame(frame, netId => this.IsActive(current) && this.TryRecreate(netId));
        }
        if (frame.DespawnedEntities != null)
        {
            foreach (var despawn in frame.DespawnedEntities)
            {
                if (!this.IsActive(current))
                {
                    return;
                }
                if (despawn != null)
                {
                    this.Remove(despawn.NetId, despawn.Reason);
                }
            }
        }
        if (!this.IsActive(current))
        {
            return;
        }
        this.Reconcile(frame.Entities);
        if (frame.EntityEvents != null)
        {
            foreach (var evt in frame.EntityEvents)
            {
                if (!this.IsActive(current))
                {
                    return;
                }
                if (evt == null || evt.Epoch != frame.Epoch)
                {
                    continue;
                }
                if (this.handlers.TryGetValue(evt.EntityKind, out var handler))
                {
                    handler.OnEntityEvent(evt);
                }
                if (this.IsActive(current))
                {
                    this.DebugLabels.OnEntityEvent(evt);
                }
            }
        }
    }

    public void Tick(long nowMs)
    {
        if (this.disposed || this.clearing)
        {
            return;
        }
        var current = this.generation;
        // Native callbacks can remove records while a pending creation is running.
        foreach (var record in this.Store.Records.Where(record => !record.Created).ToArray())
        {
            if (!this.IsActive(current))
            {
                return;
            }
            this.TryCreate(record);
        }
        foreach (var handler in this.handlers.Values)
        {
            if (!this.IsActive(current))
            {
                return;
            }
            handler.Tick(nowMs);
        }
        if (this.IsActive(current))
        {
            this.DebugLabels.Tick(nowMs);
        }
    }

    private bool TryCreate(ClientEntityRecord record)
    {
        if (this.disposed || this.clearing || !this.Store.Contains(record))
        {
            return false;
        }
        if (record.Native is Entity { destroyed: true })
        {
            this.ReleaseNative(record, EntityDespawnReason.Removed);
        }
        if (record.Native != null)
        {
            return true;
        }
        if (this.disposed || this.clearing || !this.Store.Contains(record)
            || !this.handlers.TryGetValue(record.Kind, out var handler))
        {
            return false;
        }

        var current = this.generation;
        if (!handler.TryCreate(record.Spawn, out var native) || native == null)
        {
            return false;
        }
        // Creation may re-enter a world reset/disposal before returning a native instance.
        if (!this.IsActive(current) || !this.Store.TryBind(record, native))
        {
            if (!this.Store.IsReplicated(native))
            {
                handler.Despawn(record.NetId, native, EntityDespawnReason.LevelUnloaded);
            }
            return false;
        }
        try
        {
            // Modifiers need the NetId mapping during their initialization.
            handler.OnCreated(record.Spawn, native);
            if (!this.IsActive(current) || !ReferenceEquals(record.Native, native))
            {
                return false;
            }
            this.DebugLabels.OnCreated(record);
            return true;
        }
        catch (Exception initializationError)
        {
            if (ReferenceEquals(record.Native, native))
            {
                try
                {
                    this.ReleaseNative(record, EntityDespawnReason.LevelUnloaded);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(initializationError, cleanupError);
                }
            }
            throw;
        }
    }

    private bool TryRecreate(uint netId)
    {
        if (this.disposed || this.clearing || this.Store.GetRecord(netId) is not { } record
            || record.Native is Entity { destroyed: false })
        {
            return false;
        }
        if (record.Native != null)
        {
            this.ReleaseNative(record, EntityDespawnReason.Removed);
        }
        return this.TryCreate(record);
    }

    private void Remove(uint netId, EntityDespawnReason reason)
    {
        if (this.Store.Remove(netId) is { } record)
        {
            this.ReleaseNative(record, reason);
        }
    }

    private void ReleaseNative(ClientEntityRecord record, EntityDespawnReason reason)
    {
        var native = this.Store.Unbind(record);
        try
        {
            if (this.handlers.TryGetValue(record.Kind, out var handler))
            {
                handler.Despawn(record.NetId, native, reason);
            }
        }
        finally
        {
            this.DebugLabels.OnRemoved(record);
        }
    }

    private void Reconcile(IReadOnlyList<EntityDynamic>? entities)
    {
        if (entities == null)
        {
            return;
        }
        var current = this.generation;
        var authoritative = entities.Where(entity => entity != null).Select(entity => entity.NetId).ToHashSet();
        // Preserve the existing policy: pending records wait for their level.
        foreach (var record in this.Store.Records.Where(record => record.Created
            && !authoritative.Contains(record.NetId)).ToArray())
        {
            if (!this.IsActive(current))
            {
                return;
            }
            this.Remove(record.NetId, EntityDespawnReason.Reconciled);
        }
    }

    public void Clear()
    {
        if (!this.disposed)
        {
            this.ClearCore();
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }
        this.disposed = true;
        this.ClearCore();
    }

    private bool IsActive(int expectedGeneration)
        => !this.disposed && !this.clearing && this.generation == expectedGeneration;

    private void ClearCore()
    {
        if (this.clearing)
        {
            return;
        }
        this.clearing = true;
        this.generation++;
        this.lastEpoch = -1;
        List<Exception>? errors = null;
        try
        {
            // Kind-specific cleanup state stays valid until all its natives are released.
            foreach (var record in this.Store.Records.ToArray())
            {
                try
                {
                    this.Remove(record.NetId, EntityDespawnReason.LevelUnloaded);
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add(ex);
                }
            }
            foreach (var handler in this.handlers.Values)
            {
                try
                {
                    handler.OnEpochReset();
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add(ex);
                }
            }
            try
            {
                this.DebugLabels.Reset();
            }
            catch (Exception ex)
            {
                (errors ??= []).Add(ex);
            }
        }
        finally
        {
            this.clearing = false;
        }
        if (errors != null)
        {
            throw new AggregateException("Entity pipeline cleanup failed", errors);
        }
    }
}
