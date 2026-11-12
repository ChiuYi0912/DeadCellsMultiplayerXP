namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using dc.en;
using DeadCellsMultiplayerX.Client.World.Entities;
using DeadCellsMultiplayerX.Contracts.Entities;

/// <summary>
/// Mob 类型的客户端门面
/// 实际映射由通用 ClientEntityStore 维护
/// </summary>
internal sealed class ClientMobFrameReceiver(ClientEntityStore store)
{
    private readonly ClientEntityStore store = store;
    private readonly HashSet<Mob> suppressedDeaths = [];

    public IEnumerable<Mob> All
    {
        get
        {
            foreach (var record in this.store.Records)
            {
                if (record.Kind == EntityKind.Mob && record.Native is Mob mob)
                {
                    yield return mob;
                }
            }
        }
    }

    public bool IsRemoteMob(Mob mob)
        => mob != null && this.store.IsReplicated(mob);

    public bool IsDeathSuppressed(Mob mob)
        => mob != null && this.suppressedDeaths.Contains(mob);

    public Mob? Get(uint netId)
        => this.store.Get(netId) as Mob;

    public bool TryGetNetId(Mob mob, out uint netId)
        => this.store.TryGetNetId(mob, out netId);

    public bool HasOrPending(uint netId)
        => this.store.HasOrPending(netId);

    public bool IsMissingOrDestroyed(uint netId)
        => this.store.IsMissingOrDestroyed(netId);

    internal void SuppressDeath(Mob mob)
    {
        if (mob != null)
        {
            this.suppressedDeaths.Add(mob);
        }
    }

    internal void UnsuppressDeath(Mob mob)
    {
        if (mob != null)
        {
            this.suppressedDeaths.Remove(mob);
        }
    }

    internal void ClearSuppressed() => this.suppressedDeaths.Clear();
}
