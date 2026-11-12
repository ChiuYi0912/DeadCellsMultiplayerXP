namespace DeadCellsMultiplayerX.Server.Replication.Entities;

using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using EntityMob = dc.en.Mob;

/// <summary>
/// 服务端通用实体注册表
/// 负责 NetId 分配、生成数据缓存与生命周期标记
/// </summary>
internal sealed class ServerEntityRegistry
{
    private readonly Dictionary<uint, ServerEntityEntry> byId = [];
    private readonly Dictionary<object, uint> byNative =
        [with(ReferenceEqualityComparer.Instance)];
    private readonly Dictionary<uint, EntityDespawn> removed = [];
    private readonly NetIdAllocator allocator = new();

    public int Count => this.byId.Count;

    public IReadOnlyCollection<ServerEntityEntry> Entries => this.byId.Values;

    /// <summary>
    /// 注册实体，同一原生实体只分配一次 NetId
    /// </summary>
    public uint Register(
        EntityKind kind, object native, object? descriptor, Level? level)
    {
        if (native == null)
        {
            return 0;
        }

        if (this.byNative.TryGetValue(native, out var existing))
        {
            if (this.byId.TryGetValue(existing, out var entry))
            {
                if (descriptor != null)
                {
                    entry.Descriptor = descriptor;
                }

                UpdateLevel(entry, level);
            }

            return existing;
        }

        var netId = this.allocator.Next();
        var newEntry = new ServerEntityEntry
        {
            NetId = netId,
            Kind = kind,
            Native = native,
            Descriptor = descriptor,
            Spawn = new EntitySpawn { NetId = netId, Kind = kind },
        };

        this.byId[netId] = newEntry;
        this.byNative[native] = netId;
        UpdateLevel(newEntry, level);
        return netId;
    }

    /// <summary>
    /// Mob 便捷注册入口
    /// </summary>
    public uint Register(EntityMob mob, dc.level.Mob? descriptor, Level? level)
        => this.Register(EntityKind.Mob, mob, descriptor, level);

    /// <summary>
    /// 移除实体并记录移除原因
    /// </summary>
    public void Remove(object native, EntityDespawnReason reason)
    {
        if (native == null)
        {
            return;
        }

        if (!this.byNative.TryGetValue(native, out var netId))
        {
            return;
        }

        this.byNative.Remove(native);
        if (!this.byId.Remove(netId, out var entry))
        {
            return;
        }

        if (entry.SpawnBroadcast)
        {
            this.removed[netId] = new EntityDespawn
            {
                NetId = netId,
                Kind = entry.Kind,
                Reason = reason,
            };
        }
    }

    public void MarkDead(EntityMob mob)
        => this.Remove(mob, EntityDespawnReason.Killed);

    public void Unregister(EntityMob mob)
        => this.Remove(mob, EntityDespawnReason.Removed);

    public bool TryGetNetId(object native, out uint netId)
    {
        if (native != null && this.byNative.TryGetValue(native, out netId))
        {
            return true;
        }

        netId = 0;
        return false;
    }

    public bool TryGetNetId(EntityMob mob, out uint netId)
        => this.TryGetNetId((object)mob, out netId);

    public bool TryGet(uint netId, out ServerEntityEntry entry)
        => this.byId.TryGetValue(netId, out entry!);

    public bool TryGetMob(uint netId, out EntityMob mob)
    {
        if (this.byId.TryGetValue(netId, out var entry)
            && entry.Native is EntityMob typed)
        {
            mob = typed;
            return true;
        }

        mob = null!;
        return false;
    }

    public List<EntityDespawn> DrainRemoved()
    {
        if (this.removed.Count == 0)
        {
            return [];
        }

        var result = new List<EntityDespawn>(this.removed.Values);
        this.removed.Clear();
        return result;
    }

    /// <summary>
    /// 从实体或关卡刷新关卡信息
    /// </summary>
    public static void UpdateLevel(ServerEntityEntry entry, Level? level)
    {
        var target = level ?? (entry.Native as EntityMob)?._level;
        if (target == null)
        {
            return;
        }

        entry.Spawn.LevelId = target.map?.id?.ToString() ?? entry.Spawn.LevelId;
        entry.Spawn.SubLevelIndex = target.GetSubLevelIndex();
    }

    /// <summary>
    /// 世界重置
    /// </summary>
    public void ResetWorld()
    {
        this.byId.Clear();
        this.byNative.Clear();
        this.removed.Clear();
        this.allocator.Reset();
    }
}
