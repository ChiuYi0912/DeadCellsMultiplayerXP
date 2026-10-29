using System.Collections.Generic;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Utils;
using EntityMob = dc.en.Mob;
using LevelMob = dc.level.Mob;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Hubs
{
    /// <summary>
    /// 服务端权威怪物条目
    /// </summary>
    internal sealed class ServerMobEntry
    {
        /// <summary>
        /// 网络实体 id
        /// </summary>
        public uint NetId;

        /// <summary>
        /// 服务端权威怪物实体
        /// </summary>
        public EntityMob Mob = null!;

        /// <summary>
        /// 关卡中的怪物原始数据
        /// </summary>
        public LevelMob? Descriptor;

        /// <summary>
        /// 待广播的生成数据
        /// </summary>
        public MobSpawn Spawn = new();

        /// <summary>
        /// 实体数据是否已序列化
        /// </summary>
        public bool SpawnSerialized;

        /// <summary>
        /// 生成数据是否已广播
        /// </summary>
        public bool SpawnBroadcast;
    }

    /// <summary>
    /// 服务端怪物寄存器
    /// 负责 NetId 分配 生成数据缓存 与生命周期标记
    /// </summary>
    internal sealed class ServerMobRegistry
    {
        private readonly Dictionary<uint, ServerMobEntry> byId = [];
        private readonly Dictionary<EntityMob, uint> byMob = [];
        private readonly HashSet<uint> removed = [];
        private uint nextNetId = 1;

        public int Count => byId.Count;

        public IReadOnlyCollection<ServerMobEntry> Entries => byId.Values;

        /// <summary>
        /// 注册服务端怪物 同一实体只分配一次 NetId
        /// </summary>
        public uint Register(EntityMob mob, LevelMob? descriptor, Level? level)
        {
            if (mob == null) return 0;

            if (byMob.TryGetValue(mob, out var existing))
            {
                if (byId.TryGetValue(existing, out var entry))
                {
                    if (descriptor != null)
                        entry.Descriptor = descriptor;

                    UpdateLevel(entry, level);
                }

                return existing;
            }

            uint netId = nextNetId++;
            var newEntry = new ServerMobEntry
            {
                NetId = netId,
                Mob = mob,
                Descriptor = descriptor,
                Spawn = new MobSpawn { NetId = netId },
            };

            byId[netId] = newEntry;
            byMob[mob] = netId;
            UpdateLevel(newEntry, level);
            return netId;
        }

        /// <summary>
        /// 标记怪物死亡
        /// </summary>
        public void MarkDead(EntityMob mob) => Remove(mob);

        /// <summary>
        /// 注销怪物
        /// </summary>
        public void Unregister(EntityMob mob) => Remove(mob);

        private void Remove(EntityMob mob)
        {
            if (mob == null) return;
            if (!byMob.TryGetValue(mob, out var netId)) return;

            byMob.Remove(mob);
            byId.Remove(netId);
            removed.Add(netId);
        }

        /// <summary>
        /// 通过实体反查网络 id
        /// </summary>
        public bool TryGetNetId(EntityMob mob, out uint netId)
        {
            if (mob != null && byMob.TryGetValue(mob, out netId))
                return true;

            netId = 0;
            return false;
        }

        public bool TryGet(uint netId, out EntityMob mob)
        {
            if (byId.TryGetValue(netId, out var entry))
            {
                mob = entry.Mob;
                return true;
            }

            mob = null!;
            return false;
        }

        public bool TryGetEntry(uint netId, out ServerMobEntry entry)
            => byId.TryGetValue(netId, out entry!);

        public List<uint> DrainRemoved()
        {
            if (removed.Count == 0) return [];

            var result = new List<uint>(removed);
            removed.Clear();
            return result;
        }

        /// <summary>
        /// 从实体或关卡刷新关卡信息
        /// </summary>
        public void UpdateLevel(ServerMobEntry entry, Level? level)
        {
            var target = level ?? entry.Mob?._level;
            if (target != null)
            {
                entry.Spawn.LevelId = target.map?.id?.ToString() ?? entry.Spawn.LevelId;
                entry.Spawn.SubLevelIndex = target.GetSubLevelIndex();
            }
        }

        /// <summary>
        /// 世界重载 清空全部状态并重置 NetId
        /// </summary>
        public void BeginEpoch()
        {
            byId.Clear();
            byMob.Clear();
            removed.Clear();
            nextNetId = 1;
        }
    }
}
