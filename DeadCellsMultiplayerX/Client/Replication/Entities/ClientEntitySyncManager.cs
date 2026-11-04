using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Client.Replication.Entities
{
    /// <summary>
    /// 客户端通用实体帧管线
    /// </summary>
    internal sealed class ClientEntitySyncManager
    {
        private readonly ClientEntityStore store;
        private int lastEpoch = -1;

        public ClientEntitySyncManager(ClientEntityStore store)
        {
            this.store = store;
        }

        public ClientEntityStore Store => store;

        public void ApplyFrame(WorldFrame frame)
        {
            if (frame == null) return;

            if (frame.Epoch != lastEpoch)
            {
                lastEpoch = frame.Epoch;
                foreach (var handler in store.Handlers)
                    handler.OnEpochReset(store.Context);

                store.Clear();
            }

            foreach (var handler in store.Handlers)
                handler.BeforeSpawns(frame, store.Context);

            if (frame.SpawnedEntities != null)
            {
                foreach (var spawn in frame.SpawnedEntities)
                    store.OnSpawned(spawn);
            }

            foreach (var handler in store.Handlers)
                handler.AfterSpawns(frame, store.Context);

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
                handler.AfterFrame(frame, store.Context);
        }

        public void Tick(long nowMs)
        {
            store.RetryPending();

            foreach (var handler in store.Handlers)
                handler.Tick(store.Context, nowMs);
        }

        public void Clear()
        {
            foreach (var handler in store.Handlers)
                handler.OnEpochReset(store.Context);

            store.Clear();
            lastEpoch = -1;
        }
    }
}
