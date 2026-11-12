using MessagePack;
namespace DeadCellsMultiplayerX.Contracts.Loadout
{
    [MessagePackObject]
    public sealed class EquipmentSlot
    {
        [Key(0)] public ItemDef? Def { get; set; }

        [Key(1)] public ItemRuntime Runtime { get; set; } = new();
    }
}
