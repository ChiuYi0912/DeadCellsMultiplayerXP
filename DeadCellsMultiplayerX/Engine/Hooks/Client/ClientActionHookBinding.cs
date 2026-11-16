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
        var group = new HookGroup();
        try
        {
            group.Subscribe(() => Hook_HeroActiveSkillsManager.onActiveSkill += sink.OnActiveSkill, () => Hook_HeroActiveSkillsManager.onActiveSkill -= sink.OnActiveSkill);
            group.Subscribe(() => Hook_BeheadedWeaponsManager.onWeaponExecute += sink.OnWeaponExecute, () => Hook_BeheadedWeaponsManager.onWeaponExecute -= sink.OnWeaponExecute);
            group.Subscribe(() => Hook_BaseShield.onShieldChargeStart += sink.OnShieldChargeStart, () => Hook_BaseShield.onShieldChargeStart -= sink.OnShieldChargeStart);
            group.Subscribe(() => Hook_BaseShield.onShieldReleased += sink.OnShieldReleased, () => Hook_BaseShield.onShieldReleased -= sink.OnShieldReleased);
            group.Subscribe(() => Hook_BaseShield.startParry += sink.OnStartParry, () => Hook_BaseShield.startParry -= sink.OnStartParry);
            group.Subscribe(() => Hook_BaseShield.onShieldEndParry += sink.OnShieldEndParry, () => Hook_BaseShield.onShieldEndParry -= sink.OnShieldEndParry);
        }
        catch (Exception installError)
        {
            try
            {
                group.Dispose();
            }
            catch (Exception cleanupError) { throw new AggregateException(installError, cleanupError); }
            throw;
        }
        return group;
    }
}
