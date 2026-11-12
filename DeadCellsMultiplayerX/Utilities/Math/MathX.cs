namespace DeadCellsMultiplayerX.Utilities.Math;

public static class MathX
{
    public static double Lerp(double a, double b, double t) => a + ((b - a) * t);
}
