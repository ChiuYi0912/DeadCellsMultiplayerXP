using dc.tool;
using dc.tool.atk;
using dc.tool.hero;
using dc.tool.hero.weap;
using dc.tool.weap;

namespace DeadCellsMultiplayerX.Engine.Hooks.Client
{
    /// <summary>
    /// 本地动作与背包 Hook 的端侧接收者。
    /// </summary>
    internal interface IClientActionHookSink
    {
        void OnActiveSkill(
            Hook_HeroActiveSkillsManager.orig_onActiveSkill orig,
            HeroActiveSkillsManager self,
            int id,
            double ratio);

        void OnWeaponExecute(
            Hook_BeheadedWeaponsManager.orig_onWeaponExecute orig,
            BeheadedWeaponsManager self,
            dc.tool.Weapon weapon);

        InventItem OnInventoryAdd(
            Hook_Inventory.orig_add orig,
            Inventory self,
            InventItem item);

        void OnShieldChargeStart(
            Hook_BaseShield.orig_onShieldChargeStart orig,
            BaseShield self);

        void OnShieldReleased(
            Hook_BaseShield.orig_onShieldReleased orig,
            BaseShield self);

        void OnStartParry(
            Hook_BaseShield.orig_startParry orig,
            BaseShield self);

        void OnShieldEndParry(
            Hook_BaseShield.orig_onShieldEndParry orig,
            BaseShield self);

        void OnShieldBlock(
            Hook_BaseShield.orig_onShieldBlock orig,
            BaseShield self,
            AttackData sourceAtk,
            bool fullParry);

        void OnShieldCounterAttack(
            Hook_BaseShield.orig_shieldCounterAttack orig,
            BaseShield self,
            AttackData sourceAtk,
            bool fullParry);
    }
}
