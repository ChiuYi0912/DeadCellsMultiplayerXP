namespace DeadCellsMultiplayerX.Contracts.Loadout;

[MessagePack.MessagePackObject]
public struct EquipmentSlotKey(EquipmentCategory category, int index) : IEquatable<EquipmentSlotKey>
{
    [MessagePack.Key(0)]
    public EquipmentCategory Category { get; set; } = category;

    [MessagePack.Key(1)]
    public int Index { get; set; } = index;

    public readonly bool Equals(EquipmentSlotKey other)
        => this.Category == other.Category && this.Index == other.Index;

    public override bool Equals(object? obj)
        => obj is EquipmentSlotKey other && this.Equals(other);

    public override readonly int GetHashCode()
        => HashCode.Combine((byte)this.Category, this.Index);

    public override readonly string ToString()
        => $"{this.Category}#{this.Index}";

    public static bool operator ==(EquipmentSlotKey left, EquipmentSlotKey right)
        => left.Equals(right);

    public static bool operator !=(EquipmentSlotKey left, EquipmentSlotKey right)
        => !left.Equals(right);
}
