namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc.tool.hero;
using dc.tool.hero.weap;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 本地动作 Hook 的唯一订阅入口。
/// </summary>
internal static class ClientActionHookBinding
{
    public static IDisposable Install(IClientActionHookSink sink)
    {
        Hook_HeroActiveSkillsManager.onActiveSkill += sink.OnActiveSkill;
        Hook_BeheadedWeaponsManager.onWeaponExecute += sink.OnWeaponExecute;
        Hook_BaseShield.onShieldChargeStart += sink.OnShieldChargeStart;
        Hook_BaseShield.onShieldReleased += sink.OnShieldReleased;
        Hook_BaseShield.startParry += sink.OnStartParry;
        Hook_BaseShield.onShieldEndParry += sink.OnShieldEndParry;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_HeroActiveSkillsManager.onActiveSkill -= sink.OnActiveSkill));
        group.Add(new HookLease(() => Hook_BeheadedWeaponsManager.onWeaponExecute -= sink.OnWeaponExecute));
        group.Add(new HookLease(() => Hook_BaseShield.onShieldChargeStart -= sink.OnShieldChargeStart));
        group.Add(new HookLease(() => Hook_BaseShield.onShieldReleased -= sink.OnShieldReleased));
        group.Add(new HookLease(() => Hook_BaseShield.startParry -= sink.OnStartParry));
        group.Add(new HookLease(() => Hook_BaseShield.onShieldEndParry -= sink.OnShieldEndParry));
        return group;
    }
}
