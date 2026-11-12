using MessagePack;
namespace DeadCellsMultiplayerX.Contracts.Loadout
{
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

        public EquipmentSlot? Get(EquipmentSlotKey key)
        {
            return key.Category switch
            {
                EquipmentCategory.Weapon =>
                    key.Index >= 0 && key.Index < Weapons.Count
                        ? Weapons[key.Index]
                        : null,
                EquipmentCategory.Active =>
                    key.Index >= 0 && key.Index < Actives.Count
                        ? Actives[key.Index]
                        : null,
                EquipmentCategory.Talisman => Talisman,
                _ => null,
            };
        }
    }
}
