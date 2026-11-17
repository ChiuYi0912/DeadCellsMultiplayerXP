namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Utilities.Math;

/// <summary>
/// 远程角色持有的运动组件。使用世界帧时间线提供的采样结果，
/// 将插值位置、速度、朝向与位置修正应用到所属实体。
/// </summary>
internal sealed class RemoteHeroMotion(RemoteHero owner)
{
    private readonly RemoteHero owner = owner;

    public const double TeleportDistanceSq = 25.0 * 25.0; // 25 tile
    public const double PositionCorrectionStrength = 2.0;
    public const double VelocityResponse = 0.25;

    public void Apply(PosVector previous, PosVector next, double alpha)
    {
        if (!this.owner.IsActive || previous == null || next == null)
        {
            return;
        }

        var snapX = MathX.Lerp(previous.CX + previous.XR, next.CX + next.XR, alpha);
        var snapY = MathX.Lerp(previous.CY + previous.XY, next.CY + next.XY, alpha);
        var netDx = MathX.Lerp(previous.DX, next.DX, alpha);
        var netDy = MathX.Lerp(previous.DY, next.DY, alpha);
        var netBdx = MathX.Lerp(previous.BDX, next.BDX, alpha);
        var netBdy = MathX.Lerp(previous.BDY, next.BDY, alpha);
        var targetDir = alpha < 0.5 ? previous.DIR : next.DIR;

        var actualX = this.owner.cx + this.owner.xr;
        var actualY = this.owner.cy + this.owner.yr;
        var errX = snapX - actualX;
        var errY = snapY - actualY;

        // 瞬移
        if ((errX * errX) + (errY * errY) > TeleportDistanceSq)
        {
            this.SetPosition(snapX, snapY);
            this.owner.dx = netDx;
            this.owner.dy = netDy;
            this.owner.bdx = 0;
            this.owner.bdy = 0;
            if (this.owner.dir != next.DIR)
            {
                this.owner.dir = next.DIR;
            }

            return;
        }

        // 朝向
        if (this.owner.dir != targetDir)
        {
            this.owner.dir = targetDir;
        }

        // 位置修正,误差驱动目标速度
        var targetDx = netDx + (errX * PositionCorrectionStrength);
        var targetDy = netDy + (errY * PositionCorrectionStrength);
        this.owner.dx += (targetDx - this.owner.dx) * VelocityResponse;
        this.owner.dy += (targetDy - this.owner.dy) * VelocityResponse;
        this.owner.bdx = netBdx;
        this.owner.bdy = netBdy;
    }

    public void SnapTo(PosVector pos)
    {
        if (!this.owner.IsActive || pos == null)
        {
            return;
        }

        this.SetPosition(pos.CX + pos.XR, pos.CY + pos.XY);
        this.owner.dx = pos.DX;
        this.owner.dy = pos.DY;
        this.owner.bdx = 0;
        this.owner.bdy = 0;
        if (this.owner.dir != pos.DIR)
        {
            this.owner.dir = pos.DIR;
        }
    }

    private void SetPosition(double x, double y)
    {
        var cx = (int)Math.Floor(x);
        var cy = (int)Math.Floor(y);
        this.owner.setPosCase(cx, cy, x - cx, y - cy);
    }
}
