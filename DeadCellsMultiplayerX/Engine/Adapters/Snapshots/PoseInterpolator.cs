namespace DeadCellsMultiplayerX.Engine.Adapters.Snapshots;

using dc;
using DeadCellsMultiplayerX.Contracts.Snapshots;

internal readonly record struct PoseSample(
    double X,
    double Y,
    int Cx,
    int Cy,
    double Xr,
    double Yr,
    int Dir);

/// <summary>
/// The one place that converts a quantized <see cref="PosVector"/> pair into
/// an interpolated render position.  Entity-specific velocity/error handling
/// stays in the caller.
/// </summary>
internal static class PoseInterpolator
{
    public static PoseSample Interpolate(
        PosVector previous,
        PosVector next,
        double alpha)
    {
        var previousX = previous.CX + previous.XR;
        var previousY = previous.CY + previous.XY;
        var nextX = next.CX + next.XR;
        var nextY = next.CY + next.XY;

        var x = previousX + ((nextX - previousX) * alpha);
        var y = previousY + ((nextY - previousY) * alpha);

        var cx = (int)System.Math.Floor(x);
        var cy = (int)System.Math.Floor(y);

        return new PoseSample(
            x, y,
            cx, cy,
            x - cx, y - cy,
            alpha < 0.5 ? previous.DIR : next.DIR);
    }

    public static void SetPosition(Entity target, double x, double y)
    {
        var cx = (int)System.Math.Floor(x);
        var cy = (int)System.Math.Floor(y);
        target.setPosCase(cx, cy, x - cx, y - cy);
    }
}
