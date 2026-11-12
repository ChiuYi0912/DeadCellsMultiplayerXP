using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
namespace DeadCellsMultiplayerX.Client.World.Entities
{
    /// <summary>
    /// 客户端通用实体帧管线
    /// </summary>
    internal sealed class ClientEntityFrameApplier
    {
        private readonly ClientEntityStore store;
        private readonly EntityDebugLabelManager debugLabels;

        private int lastEpoch = -1;

        public ClientEntityFrameApplier(ClientEntityStore store)
        {
            this.store = store;

            debugLabels = new EntityDebugLabelManager();
            store.RecordCreated += debugLabels.OnCreated;
            store.RecordRemoved += debugLabels.OnRemoved;
        }

        public ClientEntityStore Store => store;

        public EntityDebugLabelManager DebugLabels => debugLabels;

        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            if (frame.Epoch != lastEpoch)
            {
                lastEpoch = frame.Epoch;
                foreach (var handler in store.Handlers)
                    handler.OnEpochReset();

                debugLabels.Reset();
                store.Clear();
            }

            foreach (var handler in store.Handlers)
                handler.BeforeSpawns(frame);

            if (frame.SpawnedEntities != null)
            {
                foreach (var spawn in frame.SpawnedEntities)
                    store.OnSpawned(spawn);
            }

            foreach (var handler in store.Handlers)
                handler.AfterSpawns(frame);

            if (frame.DespawnedEntities != null)
            {
                foreach (var despawn in frame.DespawnedEntities)
                {
                    if (despawn == null) continue;
                    store.Remove(despawn.NetId, despawn.Reason);
                }
            }

            store.Reconcile(frame.Entities);

            foreach (var handler in store.Handlers)
                handler.AfterFrame(frame);

            if (frame.EntityEvents != null)
            {
                foreach (var evt in frame.EntityEvents)
                {
                    if (evt == null || evt.Epoch != frame.Epoch)
                        continue;

                    if (store.TryGetHandler(evt.EntityKind, out var handler))
                        handler.OnEntityEvent(evt);

                    debugLabels.OnEntityEvent(evt);
                }
            }
        }

        public void Tick(long nowMs)
        {
            store.RetryPending();

            foreach (var handler in store.Handlers)
                handler.Tick(nowMs);

            debugLabels.Tick(nowMs);
        }

        public void Clear()
        {
            foreach (var handler in store.Handlers)
                handler.OnEpochReset();

            debugLabels.Reset();
            store.Clear();
            lastEpoch = -1;
        }
    }
}
