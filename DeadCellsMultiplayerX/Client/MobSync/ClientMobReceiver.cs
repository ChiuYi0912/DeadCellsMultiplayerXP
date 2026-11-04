using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Client.Replication.Entities;
using DeadCellsMultiplayerX.Common.Data.Entities;

namespace DeadCellsMultiplayerX.Client.MobSync
{
    /// <summary>
    /// Mob 类型的客户端门面
    /// 实际映射由通用 ClientEntityStore 维护
    /// </summary>
    internal sealed class ClientMobReceiver
    {
        private readonly ClientEntityStore store;
        private readonly HashSet<Mob> suppressedDeaths = [];

        public ClientMobReceiver(ClientEntityStore store)
        {
            this.store = store;
        }

        public IEnumerable<Mob> All
        {
            get
            {
                foreach (var record in store.Records)
                {
                    if (record.Kind == EntityKind.Mob && record.Native is Mob mob)
                        yield return mob;
                }
            }
        }

        public bool IsRemoteMob(Mob mob)
            => mob != null && store.IsReplicated(mob);

        public bool IsDeathSuppressed(Mob mob)
            => mob != null && suppressedDeaths.Contains(mob);

        public Mob? Get(uint netId)
            => store.Get(netId) as Mob;

        public bool TryGetNetId(Mob mob, out uint netId)
            => store.TryGetNetId(mob, out netId);

        public bool HasOrPending(uint netId)
            => store.HasOrPending(netId);

        public bool IsMissingOrDestroyed(uint netId)
            => store.IsMissingOrDestroyed(netId);

        internal void SuppressDeath(Mob mob)
        {
            if (mob != null)
                suppressedDeaths.Add(mob);
        }

        internal void UnsuppressDeath(Mob mob)
        {
            if (mob != null)
                suppressedDeaths.Remove(mob);
        }

        internal void ClearSuppressed() => suppressedDeaths.Clear();
    }
}
