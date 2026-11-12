namespace DeadCellsMultiplayerX.Foundation.Versioning
{
    internal class VersionUtils
    {
        public static Version ModVersion => typeof(VersionUtils).Assembly.GetName().Version!;
    }
}
