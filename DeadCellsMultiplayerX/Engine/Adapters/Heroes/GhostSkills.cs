using dc.tool;
using dc.tool.hero;
using DeadCellsMultiplayerX.Contracts.Actions;
using Serilog;

using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes
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
    }
}
