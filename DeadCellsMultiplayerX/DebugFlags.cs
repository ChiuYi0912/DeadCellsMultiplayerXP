namespace DeadCellsMultiplayerX;

internal static class DebugFlags
{
    private static readonly HashSet<string> Flags = [];

    public static void Toggle(string key)
    {
        if (!Flags.Remove(key))
        {
            Flags.Add(key);
        }
    }

    public static bool Get(string key) => Flags.Contains(key);

    public static void Set(string key, bool on)
    {
        if (on)
        {
            Flags.Add(key);
        }
        else
        {
            Flags.Remove(key);
        }
    }

    public static void ClearViaAnsi() => Console.Write("\x1b[2J\x1b[3J\x1b[H");

}
