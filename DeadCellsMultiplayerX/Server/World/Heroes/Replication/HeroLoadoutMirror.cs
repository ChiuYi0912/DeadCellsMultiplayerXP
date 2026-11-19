namespace DeadCellsMultiplayerX.Server.World.Heroes.Replication;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
using DeadCellsMultiplayerX.Simulation.Loadout;

/// <summary>
/// 服务端保存的客户端装备快照。
/// 动作只上报槽位，物品身份与 tiers 从这里解析。
/// </summary>
internal sealed class HeroLoadoutMirror
{
    private PlayerLoadout? loadout;

    public int BrutalityTier => this.loadout?.BrutalityTier ?? 0;

    public int SurvivalTier => this.loadout?.SurvivalTier ?? 0;

    public int TacticTier => this.loadout?.TacticTier ?? 0;

    public bool Apply(PlayerLoadout? value)
    {
        if (value == null)
        {
            return false;
        }

        this.loadout = value;
        return true;
    }

    public bool TryResolve(
        EquipmentCategory category,
        int index,
        out ItemRef? item)
    {
        item = null;

        var slot = category switch
        {
            EquipmentCategory.Weapon => Get(this.loadout?.Weapons, index),
            EquipmentCategory.Active => Get(this.loadout?.Actives, index),
            EquipmentCategory.Talisman => this.loadout?.Talisman,
            _ => null,
        };

        item = LoadoutCodec.ToItemRef(slot, index);
        return item != null;
    }

    private static EquipmentSlot? Get(
        List<EquipmentSlot>? slots,
        int index)
        => slots != null && index >= 0 && index < slots.Count
            ? slots[index]
            : null;
}
