namespace DeadCellsMultiplayerX.Engine.Overrides.Weapons;

using dc;
using dc.tool.hero;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using HaxeProxy.Runtime;
using Manager = dc.tool.hero.Hook_HeroWeaponsManager;


internal static class MultiplayerWeaponsHooks
{
    public static void Register(HookGroup hooks)
    {
        hooks.Subscribe(
            () => Manager.addWeaponLock += Manager_addWeaponLock,
            () => Manager.addWeaponLock -= Manager_addWeaponLock
        );

        hooks.Subscribe(
            () => Manager.applyHudLockFade += Manager_applyHudLockFade,
            () => Manager.applyHudLockFade -= Manager_applyHudLockFade
        );

        hooks.Subscribe(
           () => Manager.removeWeaponLock += Manager_removeWeaponLock,
           () => Manager.removeWeaponLock -= Manager_removeWeaponLock
       );
    }

    private static void Manager_removeWeaponLock(Manager.orig_removeWeaponLock orig, HeroWeaponsManager self, String str, Ref<int> id)
    {
        if (self is MultiplayerWeaponsManager manager)
        {
            manager.RemoveWeaponLock(str.ToString(), id);
            return;
        }
        orig(self, str, id);
    }

    private static void Manager_applyHudLockFade(Manager.orig_applyHudLockFade orig, HeroWeaponsManager self)
    {
        if (self is MultiplayerWeaponsManager manager)
        {
            manager.ApplyHudLockFade();
            return;
        }
        orig(self);
    }

    private static void Manager_addWeaponLock(Manager.orig_addWeaponLock orig, HeroWeaponsManager self, String str, Ref<int> id)
    {
        if (self is MultiplayerWeaponsManager manager)
        {
            manager.AddWeaponLock(str.ToString(), id);
            return;
        }
        orig(self, str, id);
    }

}
