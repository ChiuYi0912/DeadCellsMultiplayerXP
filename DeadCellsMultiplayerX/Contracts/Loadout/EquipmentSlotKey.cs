namespace DeadCellsMultiplayerX.Contracts.Loadout
{
    [MessagePack.MessagePackObject]
    public struct EquipmentSlotKey : IEquatable<EquipmentSlotKey>
    {
        [MessagePack.Key(0)]
        public EquipmentCategory Category { get; set; }

        [MessagePack.Key(1)]
        public int Index { get; set; }

        public EquipmentSlotKey(EquipmentCategory category, int index)
        {
            Category = category;
            Index = index;
        }

        public bool Equals(EquipmentSlotKey other)
            => Category == other.Category && Index == other.Index;

        public override bool Equals(object? obj)
            => obj is EquipmentSlotKey other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine((byte)Category, Index);

        public override string ToString()
            => $"{Category}#{Index}";

        public static bool operator ==(EquipmentSlotKey left, EquipmentSlotKey right)
            => left.Equals(right);

        public static bool operator !=(EquipmentSlotKey left, EquipmentSlotKey right)
            => !left.Equals(right);
    }
}
