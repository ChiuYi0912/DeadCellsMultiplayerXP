namespace DeadCellsMultiplayerX.Foundation.Versioning;

internal sealed class VersionUtils
{
    public static Version ModVersion => typeof(VersionUtils).Assembly.GetName().Version!;
}
