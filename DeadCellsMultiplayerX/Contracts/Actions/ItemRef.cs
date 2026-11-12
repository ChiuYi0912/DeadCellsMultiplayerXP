namespace DeadCellsMultiplayerX.Contracts.Actions;

using MessagePack;

/// <summary>
/// 物品的可传输快照
/// </summary>
[MessagePackObject]
public class ItemRef
{
    [Key(0)] public int Kind { get; set; }

    [Key(1)] public string ItemId { get; set; } = string.Empty;

    [Key(2)] public int ItemLevel { get; set; } = 1;

    [Key(3)] public int PosID { get; set; } = -1;

    [Key(4)] public int PermanentId { get; set; }

    [Key(5)] public int Ammo { get; set; }

    [Key(6)] public List<string> Affixes { get; set; } = [];

    [Key(7)] public List<string> InactiveAffixes { get; set; } = [];
}
