using dc.tool.weap;
using dc.tool.hero.weap;
using dc.tool.hero;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
namespace DeadCellsMultiplayerX.Engine.Hooks.Client
{
    /// <summary>
    /// 本地动作与背包 Hook 的唯一订阅入口。
    /// </summary>
    internal static class ClientActionHookBinding
    {
        public static IDisposable Install(IClientActionHookSink sink)
        {
            Hook_HeroActiveSkillsManager.onActiveSkill += sink.OnActiveSkill;
            Hook_BeheadedWeaponsManager.onWeaponExecute += sink.OnWeaponExecute;
            Hook_Inventory.add += sink.OnInventoryAdd;
            Hook_BaseShield.onShieldChargeStart += sink.OnShieldChargeStart;
            Hook_BaseShield.onShieldReleased += sink.OnShieldReleased;
            Hook_BaseShield.startParry += sink.OnStartParry;
            Hook_BaseShield.onShieldEndParry += sink.OnShieldEndParry;
            Hook_BaseShield.onShieldBlock += sink.OnShieldBlock;
            Hook_BaseShield.shieldCounterAttack += sink.OnShieldCounterAttack;

            var group = new HookGroup();
            group.Add(new HookLease(() => Hook_HeroActiveSkillsManager.onActiveSkill -= sink.OnActiveSkill));
            group.Add(new HookLease(() => Hook_BeheadedWeaponsManager.onWeaponExecute -= sink.OnWeaponExecute));
            group.Add(new HookLease(() => Hook_Inventory.add -= sink.OnInventoryAdd));
            group.Add(new HookLease(() => Hook_BaseShield.onShieldChargeStart -= sink.OnShieldChargeStart));
            group.Add(new HookLease(() => Hook_BaseShield.onShieldReleased -= sink.OnShieldReleased));
            group.Add(new HookLease(() => Hook_BaseShield.startParry -= sink.OnStartParry));
            group.Add(new HookLease(() => Hook_BaseShield.onShieldEndParry -= sink.OnShieldEndParry));
            group.Add(new HookLease(() => Hook_BaseShield.onShieldBlock -= sink.OnShieldBlock));
            group.Add(new HookLease(() => Hook_BaseShield.shieldCounterAttack -= sink.OnShieldCounterAttack));
            return group;
        }
    }
}
