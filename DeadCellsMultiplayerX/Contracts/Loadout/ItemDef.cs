namespace DeadCellsMultiplayerX.Contracts.Loadout;

using MessagePack;

[MessagePackObject]
public sealed class ItemDef
{
    [Key(0)] public int Kind { get; set; }

    [Key(1)] public string ItemId { get; set; } = string.Empty;

    [Key(2)] public int Level { get; set; } = 1;

    [Key(3)] public int PermanentId { get; set; }

    [Key(4)] public List<string> Affixes { get; set; } = [];

    [Key(5)] public List<string> InactiveAffixes { get; set; } = [];
}
