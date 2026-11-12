namespace DeadCellsMultiplayerX.Client.World.Entities;

using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 客户端通用实体帧管线
/// </summary>
internal sealed class ClientEntityFrameApplier
{
    private int lastEpoch = -1;

    public ClientEntityFrameApplier(ClientEntityStore store)
    {
        this.Store = store;

        this.DebugLabels = new EntityDebugLabelManager();
        store.RecordCreated += this.DebugLabels.OnCreated;
        store.RecordRemoved += this.DebugLabels.OnRemoved;
    }

    public ClientEntityStore Store { get; }

    public EntityDebugLabelManager DebugLabels { get; }

    public void ApplyFrame(WorldFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        if (frame.Epoch != this.lastEpoch)
        {
            this.lastEpoch = frame.Epoch;
            foreach (var handler in this.Store.Handlers)
            {
                handler.OnEpochReset();
            }

            this.DebugLabels.Reset();
            this.Store.Clear();
        }

        foreach (var handler in this.Store.Handlers)
        {
            handler.BeforeSpawns(frame);
        }

        if (frame.SpawnedEntities != null)
        {
            foreach (var spawn in frame.SpawnedEntities)
            {
                this.Store.OnSpawned(spawn);
            }
        }

        foreach (var handler in this.Store.Handlers)
        {
            handler.AfterSpawns(frame);
        }

        if (frame.DespawnedEntities != null)
        {
            foreach (var despawn in frame.DespawnedEntities)
            {
                if (despawn == null)
                {
                    continue;
                }

                this.Store.Remove(despawn.NetId, despawn.Reason);
            }
        }

        this.Store.Reconcile(frame.Entities);

        foreach (var handler in this.Store.Handlers)
        {
            handler.AfterFrame(frame);
        }

        if (frame.EntityEvents != null)
        {
            foreach (var evt in frame.EntityEvents)
            {
                if (evt == null || evt.Epoch != frame.Epoch)
                {
                    continue;
                }

                if (this.Store.TryGetHandler(evt.EntityKind, out var handler))
                {
                    handler.OnEntityEvent(evt);
                }

                this.DebugLabels.OnEntityEvent(evt);
            }
        }
    }

    public void Tick(long nowMs)
    {
        this.Store.RetryPending();

        foreach (var handler in this.Store.Handlers)
        {
            handler.Tick(nowMs);
        }

        this.DebugLabels.Tick(nowMs);
    }

    public void Clear()
    {
        foreach (var handler in this.Store.Handlers)
        {
            handler.OnEpochReset();
        }

        this.DebugLabels.Reset();
        this.Store.Clear();
        this.lastEpoch = -1;
    }
}
