namespace DeadCellsMultiplayerX.Contracts.Loadout;

using MessagePack;

[MessagePackObject]
public sealed class PlayerLoadout
{
    [Key(0)] public uint Version { get; set; }

    [Key(1)] public List<EquipmentSlot> Weapons { get; set; } = [];

    [Key(2)] public List<EquipmentSlot> Actives { get; set; } = [];

    [Key(3)] public EquipmentSlot? Talisman { get; set; }

    [Key(4)] public int BrutalityTier { get; set; }

    [Key(5)] public int SurvivalTier { get; set; }

    [Key(6)] public int TacticTier { get; set; }

    public EquipmentSlot? Get(EquipmentSlotKey key) => key.Category switch
    {
        EquipmentCategory.Weapon =>
            key.Index >= 0 && key.Index < this.Weapons.Count
                ? this.Weapons[key.Index]
                : null,
        EquipmentCategory.Active =>
            key.Index >= 0 && key.Index < this.Actives.Count
                ? this.Actives[key.Index]
                : null,
        EquipmentCategory.Talisman => this.Talisman,
        _ => null,
    };
}
