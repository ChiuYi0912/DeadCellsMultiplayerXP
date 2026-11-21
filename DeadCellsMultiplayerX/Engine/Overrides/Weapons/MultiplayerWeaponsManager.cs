namespace DeadCellsMultiplayerX.Engine.Overrides.Weapons;

using CoreLibrary.Core.Extensions;
using dc.en;
using dc.hl.types;
using dc.tool.hero.weap;
using HaxeProxy.Runtime;


public abstract class MultiplayerWeaponsManager(Hero hero) : BeheadedWeaponsManager(hero)
{

    public virtual void AddWeaponLock(string str, Ref<int> id)
    {
        if (id.IsNull || id.value == -1)
        {
            foreach (var kv in this.weaponControlLocks.ToDictionary(s => (ArrayObj)s))
            {
                if (kv.Value.indexOf(str, 0) >= 0)
                {
                    continue;
                }

                kv.Value.push(str);
                //HUD.Class.ME.setWeaponFade(kv.Key, true);
            }
            return;
        }

        var target = (ArrayObj)this.weaponControlLocks.get(id.value);
        if (target.indexOf(str, 0) >= 0)
        {
            return;
        }

        target.push(str);
        //HUD.Class.ME.setWeaponFade(id.value, true);
    }

    public virtual void ApplyHudLockFade()
    {
        // for (var i = 0; i < WeaponSlotCount; i++)
        // {
        //     var slot = (ArrayObj)this.weaponControlLocks.get(i);
        //     if (slot != null && slot.length > 0)
        //     {
        //         HUD.Class.ME.setWeaponFade(i, true);
        //     }
        // }
    }

    public virtual void RemoveWeaponLock(string str, Ref<int> id)
    {
        // if (id.IsNull || id.value == -1)
        // {
        //     var i = 0;
        //     foreach (var slot in this.weaponControlLocks.ToDictionary(s => (ArrayObj)s).Values)
        //     {
        //         slot.remove(str);
        //         HUD.Class.ME.setWeaponFade(i, false);
        //         i++;
        //     }
        //     return;
        // }

        // var target = (ArrayObj)this.weaponControlLocks.get(id.value);
        // target.remove(str);
        // HUD.Class.ME.setWeaponFade(id.value, false);
    }
}
