namespace DeadCellsMultiplayerX.Client.Presentation.Heroes;

using dc;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Utilities.Math;

/// <summary>
/// 无状态的远端英雄位置应用器
/// 插值数据来自世界帧时间线,本类只负责把相邻两帧状态应用到实体
/// </summary>
internal static class RemoteHeroMotion
{
    public const double TeleportDistanceSq = 25.0 * 25.0; // 25 tile
    public const double PositionCorrectionStrength = 2.0;
    public const double VelocityResponse = 0.25;

    public static void Apply(Entity target, PosVector previous, PosVector next, double alpha)
    {
        if (target == null || previous == null || next == null)
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

        var actualX = target.cx + target.xr;
        var actualY = target.cy + target.yr;
        var errX = snapX - actualX;
        var errY = snapY - actualY;

        // 瞬移
        if ((errX * errX) + (errY * errY) > TeleportDistanceSq)
        {
            SetPosition(target, snapX, snapY);
            target.dx = netDx;
            target.dy = netDy;
            target.bdx = 0;
            target.bdy = 0;
            if (target.dir != next.DIR)
            {
                target.dir = next.DIR;
            }

            return;
        }

        // 朝向
        if (target.dir != targetDir)
        {
            target.dir = targetDir;
        }

        // 位置修正,误差驱动目标速度
        var targetDx = netDx + (errX * PositionCorrectionStrength);
        var targetDy = netDy + (errY * PositionCorrectionStrength);
        target.dx += (targetDx - target.dx) * VelocityResponse;
        target.dy += (targetDy - target.dy) * VelocityResponse;
        target.bdx = netBdx;
        target.bdy = netBdy;
    }

    public static void SnapTo(Entity target, PosVector pos)
    {
        if (target == null || pos == null)
        {
            return;
        }

        SetPosition(target, pos.CX + pos.XR, pos.CY + pos.XY);
        target.dx = pos.DX;
        target.dy = pos.DY;
        target.bdx = 0;
        target.bdy = 0;
        if (target.dir != pos.DIR)
        {
            target.dir = pos.DIR;
        }
    }

    private static void SetPosition(Entity target, double x, double y)
    {
        var cx = (int)System.Math.Floor(x);
        var cy = (int)System.Math.Floor(y);
        target.setPosCase(cx, cy, x - cx, y - cy);
    }
}
