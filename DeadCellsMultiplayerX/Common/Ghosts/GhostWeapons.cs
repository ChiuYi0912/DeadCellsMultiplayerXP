using System.Collections.Generic;
using System.Diagnostics;
using dc;
using dc.en;
using dc.tool;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// GhostHero的武器
    /// </summary>
    internal sealed class GhostWeapons
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostWeapons");

        private readonly GhostActionContext context;
        private readonly GhostLoadout loadout;
        private readonly Weapon?[] weapons = new Weapon?[2];

        public GhostWeapons(GhostActionContext context)
        {
            this.context = context;
            loadout = new GhostLoadout(context, InventItemKind.Indexes.Weapon);
        }

        public Weapon? Get(int slot) => slot is >= 0 and <= 1 ? weapons[slot] : null;

        /// <summary>
        /// 确保武器槽物品存在并创建具体 Weapon 子类
        /// </summary>
        public bool Prepare(int slot, ItemRef? snapshot)
        {
            if (!loadout.TryPrepare(slot, snapshot, out var item, out bool changed))
                return false;

            if (changed || weapons[slot] == null)
            {
                if (item == null) return false;

                Weapon? weapon = Weapon.Class.create.Invoke(context.Hero, item);

                // 防止其中一位玩家使用模组武器
                if (weapon == null)
                {
                    Logger.Warning(
                        "GhostWeapons {Owner}: unknown weapon {ItemId}",
                        context.OwnerGuid, item.kind.Index);
                    return false;
                }

                DisposeWeapon(slot);
                weapons[slot] = weapon;
            }

            return weapons[slot] != null;
        }


        /// <summary>
        /// 重放一次武器连招段
        /// </summary>
        public bool Use(int slot, int cycle)
        {
            var weapon = weapons[slot];
            if (weapon == null) return false;

            weapon.set_cycle(cycle);

            var before = context.Count();
            bool ranged = weapon.isBow() || weapon.item?.hasTag("Ranged".AsHaxeString()) == true;
            var candidates = CaptureOpponents(weapon, slot, ranged);

            GhostReplayGuard.BeginVisualBatch();
            try
            {
                weapon._executeImpl(1.0);

                if (!ranged)
                {
                    FallbackHits(weapon, candidates, slot);
                }
            }
            finally
            {
                GhostReplayGuard.EndVisualBatch();
            }

            context.LogActionDelta("weapon", slot, before);
            return true;
        }

        private List<(dc.en.Mob Mob, int Life)> CaptureOpponents(
            Weapon weapon, int slot, bool ranged)
        {
            var result = new List<(dc.en.Mob, int)>();
            var seen = new HashSet<dc.en.Mob>();
            var level = context.Hero._level;
            if (level == null) return result;

            CollectFromTeam(level.teamMob, weapon, result, seen);
            CollectFromTeam(level.teamProp, weapon, result, seen);

            var entities = level.entities;
            if (entities != null)
            {
                for (int i = 0; i < entities.length; i++)
                {
                    var mob = AffectCodec.ToObject<dc.en.Mob>(entities.getDyn(i));
                    AddCandidate(mob, weapon, result, seen);
                }
            }

            Logger.Verbose(
                "GhostHero weapon targets: owner={Owner} slot={Slot} item={Item} " +
                "teamMob={TeamMob} teamProp={TeamProp} candidates={Candidates} ranged={Ranged}",
                context.OwnerGuid, slot, weapon.item?.kind.Index,
                level.teamMob?.asMobs?.length ?? -1,
                level.teamProp?.asMobs?.length ?? -1,
                result.Count, ranged);

            return result;
        }

        private void CollectFromTeam(
            Team? team,
            Weapon weapon,
            List<(dc.en.Mob Mob, int Life)> result,
            HashSet<dc.en.Mob> seen)
        {
            var mobs = team?.asMobs;
            if (mobs == null) return;

            for (int i = 0; i < mobs.length; i++)
            {
                var mob = AffectCodec.ToObject<dc.en.Mob>(mobs.getDyn(i));
                AddCandidate(mob, weapon, result, seen);
            }
        }

        private void AddCandidate(
            dc.en.Mob? mob,
            Weapon weapon,
            List<(dc.en.Mob Mob, int Life)> result,
            HashSet<dc.en.Mob> seen)
        {
            if (mob == null || mob.destroyed || mob.life <= 0) return;

            if (!seen.Add(mob)) return;

            if (ReferenceEquals(mob._team, context.Hero._team)) return;

            if (!weapon.canHit(mob, null)) return;

            result.Add((mob, mob.life));
        }

        private void FallbackHits(
            Weapon weapon,
            List<(dc.en.Mob Mob, int Life)> candidates,
            int slot)
        {
            if (candidates.Count == 0) return;

            int hits = 0;
            foreach (var (mob, life) in candidates)
            {
                if (mob.destroyed || mob.life != life) continue;

                // 客户端只表现时不扣血，用本次批次内的已命中标记避免重复 onDamage
                if (GhostReplayGuard.WasVisualHit(mob)) continue;
                if (!weapon.canHit(mob, null)) continue;

                weapon.hitFromWeapon(mob, Ref<int>.In(-1));
                hits++;
                if (hits > 0)
                {
                    Logger.Verbose(
                        "GhostHero weapon fallback: owner={Owner} slot={Slot} item={Item} hits={Hits}",
                        context.OwnerGuid, slot, weapon.item?.kind.Index, hits);
                }
            }
        }

        private void DisposeWeapon(int slot)
        {
            var weapon = weapons[slot];
            if (weapon == null) return;

            weapon.dispose();

            weapons[slot] = null;
        }

        /// <summary>
        /// 预留给后续武器状态 tick
        /// </summary>
        public void Tick(double dt)
        {
        }

        public void Dispose()
        {
            for (int i = 0; i < weapons.Length; i++)
                DisposeWeapon(i);

            loadout.Clear();
        }

#if DEBUG
        /// <summary>
        /// 手动触发：打印 Ghost 武器附近可命中的怪物数量
        /// </summary>
        public void Diagnose(int slot)
        {
            var weapon = Get(slot);

            var team = context.Hero._team;

            int nearby = 0;
            int hittable = 0;
            int life = 0;

            var iterator = team.opponentMobsIterator.reset(team);
            int guard = 0;

            while (iterator.hasNext() && guard++ < 512)
            {
                var mob = iterator.next();
                if (mob == null || mob.destroyed || mob.life <= 0) continue;

                int dx = mob.cx - context.Hero.cx;
                int dy = mob.cy - context.Hero.cy;
                if (dx * dx + dy * dy > 144) continue;

                nearby++;
                life += mob.life;

                if (weapon!.canHit(mob, null)) hittable++;
            }

            Logger.Information(
                "GhostHero weapon diag: owner={Owner} slot={Slot} dir={Dir} " +
                "pos=({CX},{CY}) nearby={Nearby} hittable={Hittable} life={Life}",
                context.OwnerGuid, slot, context.Hero.dir,
                context.Hero.cx, context.Hero.cy,
                nearby, hittable, life);
        }
#endif
    }
}
