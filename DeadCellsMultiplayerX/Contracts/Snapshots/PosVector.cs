namespace DeadCellsMultiplayerX.Contracts.Snapshots;

using MessagePack;

[MessagePackObject]
public class PosVector
{
    [Key(0)] public ulong Packed { get; set; }
    [Key(1)] public ulong PackedVel { get; set; }

    private const int CX_BITS = 16;
    private const int CY_BITS = 16;
    private const int XR_BITS = 8;
    private const int XY_BITS = 8;

    private const ulong CX_MASK = (1UL << CX_BITS) - 1;
    private const ulong CY_MASK = ((1UL << CY_BITS) - 1) << 16;
    private const ulong DIR_MASK = 1UL << 32;
    private const ulong XR_MASK = ((1UL << XR_BITS) - 1) << 33;
    private const ulong XY_MASK = ((1UL << XY_BITS) - 1) << 41;

    private const int CY_SHIFT = 16;
    private const int XR_SHIFT = 33;
    private const int XY_SHIFT = 41;
    private const int VEL_BITS = 16;
    private const int VEL_SCALE = 4096;

    private const ulong DX_MASK = (1UL << VEL_BITS) - 1;
    private const ulong DY_MASK = ((1UL << VEL_BITS) - 1) << VEL_BITS;
    private const ulong BDX_MASK = ((1UL << VEL_BITS) - 1) << (VEL_BITS * 2);
    private const ulong BDY_MASK = ((1UL << VEL_BITS) - 1) << (VEL_BITS * 3);

    private const int DX_SHIFT = 0;
    private const int DY_SHIFT = VEL_BITS;
    private const int BDX_SHIFT = VEL_BITS * 2;
    private const int BDY_SHIFT = VEL_BITS * 3;

    [IgnoreMember]
    public int CX
    {
        get => (int)(this.Packed & CX_MASK);
        set => this.Packed = (this.Packed & ~CX_MASK) | ((ulong)value & CX_MASK);
    }

    [IgnoreMember]
    public int CY
    {
        get => (int)((this.Packed & CY_MASK) >> CY_SHIFT);
        set => this.Packed = (this.Packed & ~CY_MASK) | (((ulong)value << CY_SHIFT) & CY_MASK);
    }

    [IgnoreMember]
    public double XR
    {
        get => ((this.Packed & XR_MASK) >> XR_SHIFT) / 255.0;
        set
        {
            var quant = (byte)(value * 255.0);
            this.Packed = (this.Packed & ~XR_MASK) | ((ulong)quant << XR_SHIFT);
        }
    }

    [IgnoreMember]
    public double XY
    {
        get => ((this.Packed & XY_MASK) >> XY_SHIFT) / 255.0;
        set
        {
            var quant = (byte)(value * 255.0);
            this.Packed = (this.Packed & ~XY_MASK) | ((ulong)quant << XY_SHIFT);
        }
    }

    [IgnoreMember]
    public int DIR
    {
        get => (this.Packed & DIR_MASK) != 0 ? -1 : 1;
        set
        {
            if (value < 0)
            {
                this.Packed |= DIR_MASK;
            }
            else
            {
                this.Packed &= ~DIR_MASK;
            }
        }
    }


    [IgnoreMember]
    public double DX
    {
        get => (short)((this.PackedVel & DX_MASK) >> DX_SHIFT) / (double)VEL_SCALE;
        set => this.PackedVel = (this.PackedVel & ~DX_MASK)
                         | ((ulong)(ushort)QuantVel(value) << DX_SHIFT);
    }

    [IgnoreMember]
    public double DY
    {
        get => (short)((this.PackedVel & DY_MASK) >> DY_SHIFT) / (double)VEL_SCALE;
        set => this.PackedVel = (this.PackedVel & ~DY_MASK)
                         | ((ulong)(ushort)QuantVel(value) << DY_SHIFT);
    }

    [IgnoreMember]
    public double BDX
    {
        get => (short)((this.PackedVel & BDX_MASK) >> BDX_SHIFT) / (double)VEL_SCALE;
        set => this.PackedVel = (this.PackedVel & ~BDX_MASK)
                         | ((ulong)(ushort)QuantVel(value) << BDX_SHIFT);
    }

    [IgnoreMember]
    public double BDY
    {
        get => (short)((this.PackedVel & BDY_MASK) >> BDY_SHIFT) / (double)VEL_SCALE;
        set => this.PackedVel = (this.PackedVel & ~BDY_MASK)
                         | ((ulong)(ushort)QuantVel(value) << BDY_SHIFT);
    }

    private static int QuantVel(double v)
    {
        var q = (long)(v * VEL_SCALE);
        if (q > 32767)
        {
            q = 32767;
        }
        else if (q < -32768)
        {
            q = -32768;
        }

        return (int)q;
    }

    public PosVector() { }

    public PosVector(int cx, int cy, double xr, double xy, int dir)
    {
        this.CX = cx;
        this.CY = cy;
        this.XR = xr;
        this.XY = xy;
        this.DIR = dir;
    }

    public PosVector(int cx, int cy, double xr, double xy, int dir,
                     double dx, double dy, double bdx, double bdy)
    {
        this.CX = cx;
        this.CY = cy;
        this.XR = xr;
        this.XY = xy;
        this.DIR = dir;
        this.DX = dx;
        this.DY = dy;
        this.BDX = bdx;
        this.BDY = bdy;
    }
}
