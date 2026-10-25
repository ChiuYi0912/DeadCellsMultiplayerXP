using System;
using System.Collections.Generic;
using System.Diagnostics;
using dc;
using dc.en;
using dc.tool;
using dc.tool.hero;
using dc.tool.hero.activeSkills;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// GhostHero 的背包.技能与武器的执行
    /// </summary>
    internal sealed class GhostActions
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostActions");

        private readonly Hero hero;
        private readonly string ownerGuid;
        private readonly string?[] activeSignatures = new string?[2];
        private readonly string?[] weaponSignatures = new string?[2];
        private readonly Weapon?[] weapons = new Weapon?[2];

        public GhostActions(Hero hero, string ownerGuid)
        {
            this.hero = hero;
            this.ownerGuid = ownerGuid;
        }

        #region Loadout

        private bool TryPrepare(
            int slot,
            ItemRef? snapshot,
            InventItemKind.Indexes kind,
            string?[] signatures,
            out InventItem? item,
            out bool changed)
        {
            item = null;
            changed = false;

            if (snapshot == null || string.IsNullOrEmpty(snapshot.ItemId)) return false;
            if (slot is < 0 or > 1) return false;

            string signature = Signature(snapshot);
            var existing = GetEquipped(slot, kind);

            if (signatures[slot] == signature && existing != null)
            {
                // 同一物品不重建 保留冷却/弹药状态
                item = existing;
                return true;
            }

            try
            {
                if (dc.Data.Class.item.byId.get(snapshot.ItemId.AsHaxeString()) == null)
                {
                    Logger.Warning(
                        "GhostActions {Owner}: unknown {Kind} item {ItemId}",
                        ownerGuid, kind, snapshot.ItemId);
                    return false;
                }

                var newItem = ItemCodec.Create(snapshot, kind);
                newItem.posID = slot;

                if (existing != null)
                    hero.inventory.remove(existing);

                hero.inventory.items.push(newItem);
                signatures[slot] = signature;

                item = newItem;
                changed = true;

                Logger.Debug(
                    "GhostActions {Owner}: slot {Slot} {Kind} item {ItemId} lv{Level}",
                    ownerGuid, slot, kind, snapshot.ItemId, snapshot.ItemLevel);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "GhostActions {Owner}: set slot {Slot} {Kind} item {ItemId} failed",
                    ownerGuid, slot, kind, snapshot.ItemId);
                return false;
            }
        }

        private InventItem? GetEquipped(int slot, InventItemKind.Indexes kind)
        {
            return kind == InventItemKind.Indexes.Active
                ? hero.inventory.getActiveOn(slot)
                : hero.inventory.getEquippedWeaponOn(slot);
        }

        private static string Signature(ItemRef snapshot)
        {
            return $"{snapshot.ItemId}|{snapshot.ItemLevel}|{snapshot.PermanentId}|" +
                   $"{snapshot.Ammo}|{string.Join(',', snapshot.Affixes)}|" +
                   string.Join(',', snapshot.InactiveAffixes);
        }

        #endregion

        #region Skills

        public bool PrepareSkill(int slot, ItemRef? snapshot)
        {
            if (!TryPrepare(
                    slot, snapshot, InventItemKind.Indexes.Active,
                    activeSignatures, out var item, out bool changed))
                return false;

            var active = GetActiveSkill(slot);
            if (active == null)
            {
                Logger.Warning(
                    "GhostActions {Owner}: skill slot {Slot} unavailable", ownerGuid, slot);
                return false;
            }

            if (changed && item != null)
                active.useActiveItem(item);

            return true;
        }

        public bool IsSkillReady(int slot)
        {
            var skill = GetActiveSkill(slot)?.skill;
            if (skill == null) return false;

            try
            {
                return skill.isReady();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "GhostActions {Owner}: readiness check failed slot {Slot}",
                    ownerGuid, slot);
                return skill.coolDownF <= 0 && skill.chargeF <= 0;
            }
        }

        public HeroActiveSkill? GetActiveSkill(int slot)
        {
            try
            {
                var array = hero.activeSkillsManager?.activeSkills;
                if (array == null || slot < 0 || slot >= array.length) return null;

                var value = array.getDyn(slot);
                if (value is HeroActiveSkill typed) return typed;
                return AffectCodec.ToObject<HeroActiveSkill>(value);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "GhostActions {Owner}: get active skill {Slot} failed",
                    ownerGuid, slot);
                return null;
            }
        }

        /// <summary>
        /// 服务端权威释放 走完整 onActiveSkill
        /// </summary>
        public bool CastSkill(int slot, double ratio)
        {
            var manager = hero.activeSkillsManager;
            if (manager == null) return false;

            var before = Count();
            manager.onActiveSkill(slot, ratio);
            RestoreHudIcon(slot);
            LogActionDelta("cast", slot, before);
            return true;
        }

        /// <summary>
        /// 客户端表现重放 跳过冷却/HUD/成就信号
        /// </summary>
        public bool ReplaySkill(int slot)
        {
            if (hero.activeSkillsManager is not BeheadedActiveSkillsManager manager)
                return false;

            var item = hero.inventory.getActiveOn(slot);
            if (item == null)
            {
                Logger.Warning(
                    "GhostActions {Owner}: replay slot {Slot} has no item",
                    ownerGuid, slot);
                return false;
            }

            GetActiveSkill(slot)?.useActiveItem(item);

            var data = item._itemData;
            if (data == null)
            {
                Logger.Warning(
                    "GhostActions {Owner}: replay slot {Slot} itemData missing",
                    ownerGuid, slot);
                return false;
            }

            var before = Count();
            switch (data.group)
            {
                case 0:
                    manager.spawnDeployedTrapSkill(slot, item, data);
                    break;
                case 1:
                    manager.spawnGrenadeSkill(item, data);
                    break;
                case 2:
                    LogReplaySkip(slot, "sidekick");
                    return true;
                case 3:
                    if (data.props.mob != null)
                    {
                        LogReplaySkip(slot, "pet");
                        return true;
                    }

                    manager.spawnPowerSkill(slot, item, data);
                    break;
                default:
                    return false;
            }

            RestoreHudIcon(slot);
            LogActionDelta("replay", slot, before);
            return true;
        }

        /// <summary>
        /// 部分 Power 构造时会调用 HUD.skillPowers[slot].hideIcon()
        /// 这里把本地 HUD 对应槽位恢复 避免隐形 Hero 影响玩家 HUD
        /// </summary>
        private static void RestoreHudIcon(int slot)
        {
            var hud = dc.ui.HUD.Class.ME;
            if (hud?.skillPowers == null || slot < 0 || slot >= hud.skillPowers.length)
                return;

            if (hud.skillPowers.getDyn(slot) is dc.ui.hud.Skill skill)
                skill.showIcon();
        }

        #endregion

        #region Weapons

        /// <summary>
        /// 确保武器槽物品存在并创建具体 Weapon 子类
        /// </summary>
        public bool PrepareWeapon(int slot, ItemRef? snapshot)
        {
            if (!TryPrepare(
                    slot, snapshot, InventItemKind.Indexes.Weapon,
                    weaponSignatures, out var item, out bool changed))
                return false;

            if (changed || weapons[slot] == null)
            {
                if (item == null) return false;

                // 必须用工厂创建具体武器子类 基类 Weapon 的 onExecute 不会命中怪物
                Weapon? weapon;
                try
                {
                    weapon = Weapon.Class.create.Invoke(hero, item);
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex,
                        "GhostActions {Owner}: create weapon {ItemId} failed",
                        ownerGuid, item.kind.Index);
                    return false;
                }

                if (weapon == null)
                {
                    Logger.Warning(
                        "GhostActions {Owner}: unknown weapon {ItemId}",
                        ownerGuid, item.kind.Index);
                    return false;
                }

                DisposeWeapon(slot);
                weapons[slot] = weapon;
            }

            return weapons[slot] != null;
        }

        public Weapon? GetWeapon(int slot)
            => slot is >= 0 and <= 1 ? weapons[slot] : null;

        /// <summary>
        /// 重放一次武器连招段
        /// </summary>
        public bool UseWeapon(int slot, int cycle)
        {
            var weapon = weapons[slot];
            if (weapon == null) return false;

            weapon.set_cycle(cycle);

            var before = Count();
            bool ranged = weapon.isBow()
                || weapon.item?.hasTag("Ranged".AsHaxeString()) == true;
            var candidates = CaptureOpponents(weapon, slot, ranged);

            GhostReplayGuard.BeginVisualBatch();
            try
            {
                weapon._executeImpl(1.0);

                // Ghost 不在 Level 注册，具体武器依赖的 TeamIterator 可能因为
                // 首个敌对队伍为空而一个目标都遍历不到。这里对近战武器做一次
                // 直接 hitFromWeapon 兜底；投射物武器交给自己的 Bullet/Area。
                if (!ranged)
                {
                    ApplyFallbackHits(weapon, candidates, slot);
                }
            }
            finally
            {
                GhostReplayGuard.EndVisualBatch();
            }

            LogActionDelta("weapon", slot, before);
            return true;
        }

        private List<(dc.en.Mob Mob, int Life)> CaptureOpponents(
            Weapon weapon, int slot, bool ranged)
        {
            var result = new List<(dc.en.Mob, int)>();
            var seen = new HashSet<dc.en.Mob>();
            var level = hero._level;
            if (level == null) return result;

            // 直接读 team.asMobs，跳开 teamHero.opposingTeams / TeamIterator
            CollectFromTeam(level.teamMob, weapon, result, seen);
            CollectFromTeam(level.teamProp, weapon, result, seen);

            // 兜底扫一遍 entities，防止 Mob 不在 team.asMobs
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
                ownerGuid, slot, weapon.item?.kind.Index,
                level.teamMob?.asMobs?.length ?? -1,
                level.teamProp?.asMobs?.length ?? -1,
                result.Count, ranged);

            return result;
        }

        private void CollectFromTeam(
            dc.tool.Team? team,
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
            if (ReferenceEquals(mob._team, hero._team)) return;

            try
            {
                if (!weapon.canHit(mob, null)) return;
            }
            catch
            {
                return;
            }

            result.Add((mob, mob.life));
        }

        private void ApplyFallbackHits(
            Weapon weapon,
            List<(dc.en.Mob Mob, int Life)> candidates,
            int slot)
        {
            if (candidates.Count == 0) return;

            int hits = 0;
            foreach (var (mob, life) in candidates)
            {
                if (mob.destroyed || mob.life != life) continue;

                // 客户端只表现时不扣血 用本次批次内的已命中标记避免重复 onDamage
                if (GhostReplayGuard.WasVisualHit(mob)) continue;
                if (!weapon.canHit(mob, null)) continue;

                try
                {
                    weapon.hitFromWeapon(mob, HaxeProxy.Runtime.Ref<int>.In(-1));
                    hits++;
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex,
                        "GhostActions {Owner}: fallback hit slot {Slot} failed",
                        ownerGuid, slot);
                }
            }

            if (hits > 0)
            {
                Logger.Verbose(
                    "GhostHero weapon fallback: owner={Owner} slot={Slot} item={Item} hits={Hits}",
                    ownerGuid, slot, weapon.item?.kind.Index, hits);
            }
        }

        private void DisposeWeapon(int slot)
        {
            var weapon = weapons[slot];
            if (weapon == null) return;

            weapon.dispose();

            weapons[slot] = null;
        }

        #endregion

        #region Lifecycle

        /// <summary>
        /// 预留给后续武器/技能状态 tick
        /// </summary>
        public void Tick(double dt)
        {
        }

        public void Dispose()
        {
            DestroyPowers();

            hero.activeSkillsManager?.killEntities();

            hero.activeSkillsManager?.dispose();

            for (int i = 0; i < weapons.Length; i++)
                DisposeWeapon(i);

            Array.Clear(activeSignatures);
            Array.Clear(weaponSignatures);
        }

        /// <summary>
        /// 销毁该 Hero 释放的持续 Power
        /// Power 在 Level.powers 中持续 update 并在 owner._level 为空时会崩溃
        /// </summary>
        private void DestroyPowers()
        {
            var powers = hero._level?.powers;
            if (powers == null) return;

            for (int i = powers.length - 1; i >= 0; i--)
            {
                var power = AffectCodec.ToObject<dc.Power>(powers.getDyn(i));
                if (power == null || power.destroyed) continue;
                if (!ReferenceEquals(power.owner, hero)) continue;

                try
                {
                    power.destroy();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex,
                        "GhostActions {Owner}: destroy power failed", ownerGuid);
                }
            }
        }

        #endregion

        #region Diagnostics

        private (int Entities, int Powers) Count()
        {
            var level = hero._level;
            return (level?.entities?.length ?? 0, level?.powers?.length ?? 0);
        }

        [Conditional("DEBUG")]
        private void LogActionDelta(
            string action, int slot, (int Entities, int Powers) before)
        {
            var level = hero._level;
            int entities = level?.entities?.length ?? 0;
            int powers = level?.powers?.length ?? 0;
            string levelId = level?.map?.id?.ToString() ?? string.Empty;

            Logger.Verbose(
                "GhostHero {Action}: owner={Owner} slot={Slot} level={Level} " +
                "entities {OldEntities}->{NewEntities} powers {OldPowers}->{NewPowers}",
                action, ownerGuid, slot, levelId,
                before.Entities, entities, before.Powers, powers);
        }

        [Conditional("DEBUG")]
        private void LogReplaySkip(int slot, string kind)
        {
            Logger.Debug(
                "GhostHero {Owner}: skip {Kind} replay slot {Slot}",
                ownerGuid, kind, slot);
        }

#if DEBUG
        /// <summary>
        /// 手动触发：打印 Ghost 武器附近可命中的怪物数量
        /// </summary>
        public void DiagnoseWeapon(int slot)
        {
            var weapon = GetWeapon(slot);
            if (weapon == null)
            {
                Logger.Information(
                    "GhostHero weapon diag: owner={Owner} slot={Slot} weapon unavailable",
                    ownerGuid, slot);
                return;
            }

            var team = hero._team;
            if (team?.opponentMobsIterator == null)
            {
                Logger.Information(
                    "GhostHero weapon diag: owner={Owner} slot={Slot} team unavailable",
                    ownerGuid, slot);
                return;
            }

            int nearby = 0;
            int hittable = 0;
            int life = 0;

            var iterator = team.opponentMobsIterator.reset(team);
            int guard = 0;

            while (iterator.hasNext() && guard++ < 512)
            {
                var mob = iterator.next();
                if (mob == null || mob.destroyed || mob.life <= 0) continue;

                int dx = mob.cx - hero.cx;
                int dy = mob.cy - hero.cy;
                if (dx * dx + dy * dy > 144) continue;

                nearby++;
                life += mob.life;

                try
                {
                    if (weapon.canHit(mob, null)) hittable++;
                }
                catch
                {
                    // 诊断用
                }
            }

            Logger.Information(
                "GhostHero weapon diag: owner={Owner} slot={Slot} dir={Dir} " +
                "pos=({CX},{CY}) nearby={Nearby} hittable={Hittable} life={Life}",
                ownerGuid, slot, hero.dir, hero.cx, hero.cy,
                nearby, hittable, life);
        }
#endif

        #endregion
    }
}
