using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using MessagePack;

namespace DeadCellsMultiplayerX.Contracts.Snapshots
{
    /// <summary>
    /// Packed animation state for high-frequency network sync.
    ///
    /// Original animation fields (Speed double, Frame int, Paused bool, Plays int)
    /// are reduced to two ulong fields (128 bits on wire).  Plays precision is reduced
    /// from 32 to 31 bits, still well within practical limits.
    ///
    /// Packing layout (PackedA):
    ///   Bits 0–63 : raw IEEE-754 double bits of <see cref="AnimSpeed"/>.
    ///
    /// Packing layout (PackedB):
    ///   Bits  0–30 : AnimFrame (31-bit frame index)
    ///   Bit     31 : AnimPaused (1 = paused, 0 = playing)
    ///   Bits 32–62 : AnimPlays (31-bit play count)
    ///   Bit     63 : reserved (0)
    /// </summary>
    [MessagePackObject]
    public class AnimInfo
    {
        [Key(0)] public List<AnimTransitions> AnimTransitions { get; set; } = new();
        [Key(1)] public ulong PackedA { get; set; }
        [Key(2)] public ulong PackedB { get; set; }
        [Key(3)] public ulong PackedC { get; set; }
        [Key(4)] public ulong PackedD { get; set; }
        [Key(5)] public string GroupName { get; set; } = string.Empty;
        [Key(6)] public long StartTime { get; set; }

        public const ulong FRAME_MASK = (1UL << 31) - 1;
        public const ulong PAUSED_MASK = 1UL << 31;
        private const ulong PLAYS_MASK = ((1UL << 31) - 1) << 32;
        private const ulong CURSOR_MASK = (1UL << 32) - 1;

        [IgnoreMember]
        public double Speed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BitConverter.Int64BitsToDouble((long)PackedA);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => PackedA = (ulong)BitConverter.DoubleToInt64Bits(value);
        }

        [IgnoreMember]
        public int Frame
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(PackedB & FRAME_MASK);
            set => PackedB = (PackedB & ~FRAME_MASK) | ((ulong)value & FRAME_MASK);
        }

        [IgnoreDataMember]
        public bool Paused
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (PackedB & PAUSED_MASK) != 0;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => PackedB = value
                ? (PackedB | PAUSED_MASK)
                : (PackedB & ~PAUSED_MASK);
        }

        [IgnoreDataMember]
        public int Plays
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)((PackedB & PLAYS_MASK) >> 32);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => PackedB = (PackedB & ~PLAYS_MASK) | (((ulong)value << 32) & PLAYS_MASK);
        }

        [IgnoreDataMember]
        public double playDuration
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => BitConverter.Int64BitsToDouble((long)PackedC);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => PackedC = (ulong)BitConverter.DoubleToInt64Bits(value);
        }

        [IgnoreDataMember]
        public int animCursor
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)(PackedD & CURSOR_MASK);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => PackedD = (PackedD & ~CURSOR_MASK) | ((uint)value & CURSOR_MASK);
        }
        public AnimInfo() { }

        public AnimInfo Clone()
        {
            var clone = (AnimInfo)MemberwiseClone();
            clone.AnimTransitions = AnimTransitions;
            return clone;
        }

        public void CopyFrom(AnimInfo source)
        {
            PackedA = source.PackedA;
            PackedB = source.PackedB;
            PackedC = source.PackedC;
            PackedD = source.PackedD;
            GroupName = source.GroupName;
        }
    }
}
