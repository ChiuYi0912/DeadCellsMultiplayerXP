using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Entities;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using EntityMob = dc.en.Mob;
using LevelMob = dc.level.Mob;

namespace DeadCellsMultiplayerX.Server.World.Replication.Entities.Mobs
{
    /// <summary>
    /// Mob 类型的服务端采集处理器
    /// 保留原有 hxbit 关卡描述序列化与动态状态组装逻辑
    /// </summary>
    internal sealed class ServerMobEntityHandler : IServerEntityKindHandler
    {
        private readonly ServerEntityRegistry registry;

        public ServerMobEntityHandler(ServerEntityRegistry registry)
        {
            this.registry = registry;
        }

        public EntityKind Kind => EntityKind.Mob;

        public bool IsAlive(ServerEntityEntry entry)
            => entry.Native is EntityMob mob && !mob.destroyed;

        public bool TryPrepareSpawn(ServerEntityEntry entry)
        {
            if (entry.SpawnSerialized) return true;
            if (entry.Native is not EntityMob mob || mob.destroyed) return false;
            if (entry.Descriptor is not LevelMob descriptor) return false;

            // 等待实体初始化完成后再序列化
            if (!mob.initDone || mob.spr == null) return false;

            entry.Spawn.EntityData =
                DCMXSerializers.MessagePack.Serialize<LevelMob>(descriptor);

            registry.UpdateLevel(entry, mob._level);
            entry.SpawnSerialized = true;
            return true;
        }

        public EntityDynamic? BuildDynamic(ServerEntityEntry entry, long serverTimeMs)
        {
            if (entry.Native is not EntityMob mob) return null;

            double serverSec = serverTimeMs / 1000.0;
            var affects = CollectAffects(
                mob, serverSec, mob.cd?.baseFps ?? 60.0);

            var dyn = new EntityDynamic
            {
                NetId = entry.NetId,
                Kind = EntityKind.Mob,
                Pos = new PosVector(
                    mob.cx, mob.cy, mob.xr, mob.yr, mob.dir,
                    mob.dx, mob.dy, mob.bdx, mob.bdy),
                Life = mob.life,
                MaxLife = mob.maxLife,
                LevelId = entry.Spawn.LevelId,
                SubLevelIndex = entry.Spawn.SubLevelIndex,
                Affects = affects,
            };

            if (mob.spr != null)
            {
                var tmp = new HeroDynamic();
                HeroUtils.FillAnimInfo(tmp, mob.spr, serverTimeMs);
                dyn.Anim = tmp.Anim;
            }

            return dyn;
        }

        /// <summary>
        /// 采集服务端原生 affect 并按身份去重
        /// </summary>
        private static List<AffectEntry> CollectAffects(
            EntityMob mob, double serverSec, double baseFps)
        {
            var collected = AffectCodec.Collect(mob, serverSec, baseFps);
            if (collected.Count < 2) return collected;

            HashSet<AffectKey> seen = [];
            List<AffectEntry> result = new(collected.Count);

            foreach (var entry in collected)
            {
                if (entry == null) continue;
                if (seen.Add(entry.Key)) result.Add(entry);
            }

            return result;
        }
    }
}
