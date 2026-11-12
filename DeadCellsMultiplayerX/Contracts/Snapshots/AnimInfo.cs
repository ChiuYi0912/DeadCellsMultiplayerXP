namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using MessagePack;

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
    [Key(0)] public List<AnimTransitions> AnimTransitions { get; set; } = [];
    [Key(1)] public ulong PackedA { get; set; }
    [Key(2)] public ulong PackedB { get; set; }
    [Key(3)] public ulong PackedC { get; set; }
    [Key(4)] public ulong PackedD { get; set; }
    [Key(5)] public string GroupName { get; set; } = string.Empty;
    [Key(6)] public long StartTime { get; set; }

    public const ulong FrameMask = (1UL << 31) - 1;
    public const ulong PausedMask = 1UL << 31;
    private const ulong PlaysMask = ((1UL << 31) - 1) << 32;
    private const ulong CursorMask = (1UL << 32) - 1;

    [IgnoreMember]
    public double Speed
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitConverter.Int64BitsToDouble((long)this.PackedA);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.PackedA = (ulong)BitConverter.DoubleToInt64Bits(value);
    }

    [IgnoreMember]
    public int Frame
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(this.PackedB & FrameMask);
        set => this.PackedB = (this.PackedB & ~FrameMask) | ((ulong)value & FrameMask);
    }

    [IgnoreDataMember]
    public bool Paused
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (this.PackedB & PausedMask) != 0;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.PackedB = value
            ? (this.PackedB | PausedMask)
            : (this.PackedB & ~PausedMask);
    }

    [IgnoreDataMember]
    public int Plays
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)((this.PackedB & PlaysMask) >> 32);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.PackedB = (this.PackedB & ~PlaysMask) | (((ulong)value << 32) & PlaysMask);
    }

    [IgnoreDataMember]
    public double PlayDuration
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => BitConverter.Int64BitsToDouble((long)this.PackedC);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.PackedC = (ulong)BitConverter.DoubleToInt64Bits(value);
    }

    [IgnoreDataMember]
    public int AnimCursor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (int)(this.PackedD & CursorMask);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this.PackedD = (this.PackedD & ~CursorMask) | ((uint)value & CursorMask);
    }
    public AnimInfo() { }

    public AnimInfo Clone()
    {
        var clone = (AnimInfo)this.MemberwiseClone();
        clone.AnimTransitions = this.AnimTransitions;
        return clone;
    }

    public void CopyFrom(AnimInfo source)
    {
        this.PackedA = source.PackedA;
        this.PackedB = source.PackedB;
        this.PackedC = source.PackedC;
        this.PackedD = source.PackedD;
        this.GroupName = source.GroupName;
    }
}
