using System.Diagnostics;
using dc;
using dc.en;
using dc.tool;
using dc.tool.hero;
using dc.tool.hero.activeSkills;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// GhostHero 的主动技能：Active 物品槽、释放、重放与 Power 清理
    /// </summary>
    internal sealed class GhostSkills
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostSkills");

        private readonly GhostActionContext context;
        private readonly GhostLoadout loadout;

        public GhostSkills(GhostActionContext context)
        {
            this.context = context;
            loadout = new GhostLoadout(context, InventItemKind.Indexes.Active);
        }

        /// <summary>
        /// 释放对应技能前的物品准备
        /// </summary>
        public bool Prepare(int slot, ItemRef? snapshot)
        {
            if (!loadout.TryPrepare(
                    slot, snapshot, out var item, out bool changed))
                return false;

            var active = GetActive(slot);
            if (active == null)
            {
                Logger.Warning(
                    "GhostSkills {Owner}: skill slot {Slot} unavailable",
                    context.OwnerGuid, slot);
                return false;
            }

            if (changed && item != null)
                active.useActiveItem(item);

            return true;
        }

        /// <summary>
        /// 技能是否已就绪
        /// </summary>
        public bool IsReady(int slot)
        {
            var skill = GetActive(slot)?.skill;
            if (skill == null) return false;

            return skill.isReady();
        }

        /// <summary>
        /// 获取槽位对应的技能实例
        /// </summary>
        public HeroActiveSkill? GetActive(int slot)
        {
            var array = context.Hero.activeSkillsManager.activeSkills;
            if (array == null || slot < 0 || slot >= array.length)
                return null;

            var value = array.getDyn(slot);
            if (value is HeroActiveSkill typed) return typed;
            return AffectCodec.ToObject<HeroActiveSkill>(value);
        }

        /// <summary>
        /// 服务端权威释放
        /// </summary>
        public bool Cast(int slot, double ratio)
        {
            var manager = context.Hero.activeSkillsManager;

            var before = context.Count();
            manager.onActiveSkill(slot, ratio);
            RestoreHudIcon(slot);
            context.LogActionDelta("cast", slot, before);
            return true;
        }

        /// <summary>
        /// 客户端表现重放
        /// </summary>
        public bool Replay(int slot)
        {
            if (context.Hero.activeSkillsManager is not BeheadedActiveSkillsManager manager)
                return false;

            var item = context.Hero.inventory.getActiveOn(slot);

            GetActive(slot)?.useActiveItem(item);

            var data = item._itemData;

            var before = context.Count();
            switch (data.group)
            {
                case 0://释放放置技能
                    manager.spawnDeployedTrapSkill(slot, item, data);
                    break;
                case 1://释放手榴弹技能
                    manager.spawnGrenadeSkill(item, data);
                    break;
                case 2:
                    LogReplaySkip(slot, "sidekick");
                    return true;
                case 3://释放能力技能

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
            context.LogActionDelta("replay", slot, before);
            return true;
        }

        /// <summary>
        /// 部分 Power 构造时会调用 HUD.skillPowers[slot].hideIcon()
        /// 这里把本地 HUD 对应槽位恢复，避免隐形 Hero 影响玩家 HUD
        /// </summary>
        private static void RestoreHudIcon(int slot)
        {
            var hud = dc.ui.HUD.Class.ME;
            if (hud?.skillPowers == null || slot < 0 || slot >= hud.skillPowers.length)
                return;

            if (hud.skillPowers.getDyn(slot) is dc.ui.hud.Skill skill)
                skill.showIcon();
        }

        /// <summary>
        /// 预留给后续技能状态 tick
        /// </summary>
        public void Tick(double dt)
        {
        }

        public void Dispose()
        {
            DestroyPowers();

            context.Hero.activeSkillsManager?.killEntities();
            context.Hero.activeSkillsManager?.dispose();

            loadout.Clear();
        }

        /// <summary>
        /// 销毁该 Hero 释放的持续 Power
        /// Power 在 Level.powers 中持续 update 并在 owner._level 为空时会崩溃
        /// </summary>
        private void DestroyPowers()
        {
            var powers = context.Hero._level?.powers;
            if (powers == null) return;

            for (int i = powers.length - 1; i >= 0; i--)
            {
                var power = AffectCodec.ToObject<dc.Power>(powers.getDyn(i));
                if (power == null || power?.destroyed) continue;
                if (!ReferenceEquals(power!.owner, context.Hero)) continue;

                power.destroy();
            }
        }

        [Conditional("DEBUG")]
        private void LogReplaySkip(int slot, string kind)
        {
            Logger.Debug(
                "GhostHero {Owner}: skip {Kind} replay slot {Slot}",
                context.OwnerGuid, kind, slot);
        }
    }
}
