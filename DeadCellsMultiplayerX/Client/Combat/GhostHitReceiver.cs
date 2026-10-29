using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Utils;
using Serilog;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Combat
{
    /// <summary>
    /// 接收服务端判定的 mob 命中 Ghost 事件
    /// </summary>
    internal sealed class GhostHitReceiver
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostHitReceiver");

        private readonly GuestClientSession session;

        private readonly Dictionary<string, long> lastHitIds = new(StringComparer.Ordinal);

        private int epoch = -1;

        /// <summary>
        /// 绑定所属的客户端会话，用于读取本地 Hero 与远程 mob
        /// </summary>
        public GhostHitReceiver(GuestClientSession session)
        {
            this.session = session;
        }

        /// <summary>
        /// 处理世界帧里携带的命中事件
        /// </summary>
        public void ApplyFrame(WorldFrame frame)
        {
            if (frame?.GhostHits == null || frame.GhostHits.Count == 0)
                return;

            foreach (var hit in frame.GhostHits)
                Apply(hit);
        }

        /// <summary>
        /// 处理 owner RPC 或世界帧事件
        /// </summary>
        public void Apply(GhostHitEvent hit)
        {
            if (hit == null || string.IsNullOrEmpty(hit.Guid))
                return;

            if (hit.Epoch != epoch)
            {
                epoch = hit.Epoch;
                lastHitIds.Clear();
            }

            if (lastHitIds.TryGetValue(hit.Guid, out long lastId)
                && hit.HitId <= lastId)
                return;

            lastHitIds[hit.Guid] = hit.HitId;

            var localGuid = session.RemoteWorld?.LocalGuid;
            if (!string.Equals(hit.Guid, localGuid, StringComparison.Ordinal))
            {
                Logger.Debug(
                    "Ghost hit for remote owner: hit={HitGuid} local={LocalGuid}",
                    hit.Guid, localGuid ?? "<null>");
                return;
            }

            ApplyLocal(hit);
        }

        /// <summary>
        /// 世界重置时清空 epoch 与命中去重记录
        /// </summary>
        public void Clear()
        {
            lastHitIds.Clear();
            epoch = -1;
        }

        /// <summary>
        /// 把服务端确认的伤害交给本地原版受击管线
        /// </summary>
        private void ApplyLocal(GhostHitEvent hit)
        {
            if (hit.Damage <= 0)
            {
                Logger.Debug(
                    "Ghost hit ignored: zero damage. guid={Guid} hit={HitId}",
                    hit.Guid, hit.HitId);
                return;
            }

            var hero = session.Game?.hero;
            if (hero == null || hero.destroyed || hero.life <= 0) return;

            if (!LevelUtils.IsLocalLevel(hero, hit))
            {
                var level = hero._level;
                Logger.Warning(
                    "Ghost hit ignored: level mismatch. hit={HitLevel}#{HitSub} " +
                    "local={LocalLevel}#{LocalSub}",
                    hit.LevelId, hit.SubLevelIndex,
                    level?.map?.id?.ToString() ?? "<null>",
                    level?.GetSubLevelIndex() ?? -1);
                return;
            }

            var mob = session.RemoteWorld?.Mobs.Receiver.Get(hit.AttackerNetId);

            // 目前不依赖本地 mob 的 attackSource
            var atk = new AttackData();
            atk.init(mob, hit.Damage);
            atk.finalDmg = hit.Damage;
            atk.rawFinalDmg = hit.Damage;
            atk.hitResult = new HitResult.Normal();
            atk.dmgType = new DamageType.Physical();
            atk.lastHitTarget = hero;

            int before = hero.life;

            hero.applyAttackResult(atk);

            Logger.Information(
                "Ghost hit applied: guid={Guid} attacker={NetId} mob={Mob} " +
                "damage={Damage} life={Before}->{After}",
                hit.Guid, hit.AttackerNetId, mob?.type,
                hit.Damage, before, hero.life);
        }
    }
}
