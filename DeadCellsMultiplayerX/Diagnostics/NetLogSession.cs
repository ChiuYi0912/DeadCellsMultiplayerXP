namespace DeadCellsMultiplayerX.Diagnostics;

using System.Diagnostics;

internal static class NetLogSession
{
    private static readonly Lock CounterGate = new();
    private static readonly Dictionary<string, int> Counters = [];

    public static string Directory { get; } = CreateDirectory();

    public static TraceListener CreateJsonRpcFailureListener(string role)
    {
        int index;
        lock (CounterGate)
        {
            Counters.TryGetValue(role, out index);
            index++;
            Counters[role] = index;
        }

        var path = Path.Combine(
            Directory, $"jsonrpc_{Sanitize(role)}_{index:00}_errors.log");

        return new JsonRpcFailureTraceListener(path);
    }

    private static string CreateDirectory()
    {
        var root = ModEntry.Instance?.Info.ModRoot;
        var baseDirectory = root != null
            ? root.GetFilePath("NetLog")
            : Path.Combine(AppContext.BaseDirectory, "NetLog");

        return Path.Combine(
            baseDirectory, DateTime.Now.ToString(
                "yyyyMMdd_HHmmss_fff",
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string Sanitize(string role)
    {
        if (string.IsNullOrEmpty(role))
        {
            return "unknown";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            role = role.Replace(invalid, '_');
        }

        return role;
    }
}
