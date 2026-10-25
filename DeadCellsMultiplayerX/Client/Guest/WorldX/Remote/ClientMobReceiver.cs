using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.pr;
using dc.tool.atk;
using dc.tool.mv;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using Serilog;
using LevelMob = dc.level.Mob;

namespace DeadCellsMultiplayerX.Client.Guest.WorldX.Remote
{
    /// <summary>
    /// 客户端 Mob 接收器
    /// 负责反序列化服务端生成的怪物 并维护 NetId 映射
    /// </summary>
    internal sealed class ClientMobReceiver
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ClientMobReceiver");

        private readonly GuestClientSession session;
        private readonly RemoteWorld remoteWorld;

        /// <summary>
        /// 当前客户端所有mob
        /// </summary>
        private readonly Dictionary<uint, Mob> mobs = [];
        private readonly Dictionary<Mob, uint> netIds = [];
        private readonly Dictionary<uint, MobSpawn> pendingSpawns = [];
        private readonly HashSet<string> authoritativeLevels = [];
        private readonly HashSet<Mob> suppressedDeaths = [];

        public ClientMobReceiver(GuestClientSession session, RemoteWorld remoteWorld)
        {
            this.session = session;
            this.remoteWorld = remoteWorld;
        }

        public IEnumerable<Mob> All => mobs.Values;

        public bool IsRemoteMob(Mob mob)
            => mob != null && netIds.ContainsKey(mob);

        public bool IsDeathSuppressed(Mob mob)
            => mob != null && suppressedDeaths.Contains(mob);

        public Mob? Get(uint netId)
            => mobs.TryGetValue(netId, out var mob) ? mob : null;

        public bool TryGetNetId(Mob mob, out uint netId)
            => netIds.TryGetValue(mob, out netId);

        public bool HasOrPending(uint netId)
            => mobs.ContainsKey(netId) || pendingSpawns.ContainsKey(netId);

        /// <summary>
        /// 处理服务端发送的怪物生成数据
        /// </summary>
        public void OnSpawned(MobSpawn spawn)
        {
            if (spawn == null || spawn.EntityData == null || spawn.EntityData.Length == 0)
                return;

            pendingSpawns[spawn.NetId] = spawn;
            TryCreate(spawn);
        }

        /// <summary>
        /// 重试尚未创建成功的怪物
        /// </summary>
        public void RetryPending()
        {
            if (pendingSpawns.Count == 0) return;

            var pending = new List<MobSpawn>(pendingSpawns.Values);
            foreach (var spawn in pending)
                TryCreate(spawn);
        }

        /// <summary>
        /// 移除指定网络怪物
        /// </summary>
        public void Remove(uint netId)
        {
            pendingSpawns.Remove(netId);

            if (!mobs.Remove(netId, out var mob))
                return;

            if (mob != null)
            {
                var atk = AttackUtils.Class.createFromHero(session.Game.hero, (object)999999, null);
                atk.dmgType = new DamageType.Physical();
                atk.addTag(18);
                AttackUtils.Class.hit(atk, mob);

                netIds.Remove(mob);
            }
        }

        /// <summary>
        /// 清空全部怪物与缓存
        /// </summary>
        public void Clear()
        {
            ClearCreatedMobs();
            pendingSpawns.Clear();
            authoritativeLevels.Clear();
            suppressedDeaths.Clear();
        }

        private void TryCreate(MobSpawn spawn)
        {
            if (spawn == null || mobs.ContainsKey(spawn.NetId)) return;

            var level = LevelUtils.ResolveLevel(
                session.Game, spawn.LevelId, spawn.SubLevelIndex);
            if (level == null) return;

            // 每个关卡第一次成功接收服务端怪物时 清掉本地加载的怪物防止重复
            string levelKey = LevelKey(spawn.LevelId, spawn.SubLevelIndex);
            bool needsCleanup = !authoritativeLevels.Contains(levelKey);
            if (needsCleanup)
                ClearLocalLevelMobs(level);

            LevelMob? descriptor = DCMXSerializers.MessagePack.Deserialize<LevelMob>(spawn.EntityData);

            if (descriptor == null) return;

            Mob? mob = level.attachMob(descriptor);

            if (mob == null) return;

            mob.hasRepelling = false;
            mob.hasGravity = false;
            mob.gravity = 0;
            mob.collisionMode = new CollisionMode.None();
            mob.frict = 0;

            mobs[spawn.NetId] = mob;
            netIds[mob] = spawn.NetId;

            if (needsCleanup)
                authoritativeLevels.Add(levelKey);

            Logger.Verbose(
                "Mob spawned on client: netId={NetId} level={LevelId}#{SubLevelIndex}",
                spawn.NetId, spawn.LevelId, spawn.SubLevelIndex);
        }

        private void ClearCreatedMobs()
        {
            foreach (var mob in mobs.Values)
            {
                if (mob != null && !mob.destroyed)
                    mob.destroy();
            }

            mobs.Clear();
            netIds.Clear();
        }

        /// <summary>
        /// 清掉指定关卡客户端本地加载的怪物
        /// </summary>
        private void ClearLocalLevelMobs(Level level)
        {
            var entities = level.entities;
            if (entities == null) return;

            var toDestroy = new List<Mob>();
            for (int i = 0; i < entities.length; i++)
            {
                if (entities.getDyn(i) is Mob mob
                    && !IsRemoteMob(mob)
                    && !mob.destroyed)
                {
                    toDestroy.Add(mob);
                }
            }

            // 清理本地怪物时同样抑制本地死亡掉落
            foreach (var mob in toDestroy)
                suppressedDeaths.Add(mob);

            foreach (var mob in toDestroy)
                mob.destroy();

            foreach (var mob in toDestroy)
                suppressedDeaths.Remove(mob);
        }

        private static string LevelKey(string levelId, int subLevelIndex)
            => $"{levelId}#{subLevelIndex}";
    }
}
