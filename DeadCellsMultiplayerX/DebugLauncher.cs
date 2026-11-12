namespace DeadCellsMultiplayerX;

#if DEBUG
using System.Diagnostics;
using DeadCellsMultiplayerX.Client.Session;
using Serilog;

internal static class DebugLauncher
{
    private const string EXE_PATH =
        @"D:\steam\steamapps\common\Dead Cells\coremod\core\host\startup\DeadCellsModding.exe";

    private static ILogger Logger => field ??= Log.ForContext(nameof(DebugLauncher), nameof(DebugLauncher));


    private static string? autoAddr;
    private static string? playerName;

    public static bool IsTestClient => !string.IsNullOrEmpty(autoAddr);

    public static void Init()
    {
        var addr = Environment.GetEnvironmentVariable("DCMP_AUTOCONNECT");
        var name = Environment.GetEnvironmentVariable("DCMP_PLAYERNAME");

        if (!string.IsNullOrEmpty(addr))
        {
            autoAddr = addr;
            Logger.Information("auto-connect (env): {autoAddr}", autoAddr);
        }
        if (!string.IsNullOrEmpty(name))
        {
            playerName = name;
            Logger.Information("player name (env): {playerName}", playerName);
        }

        foreach (var a in Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("--dcmp-autoconnect=", StringComparison.Ordinal))
            {
                autoAddr = a["--dcmp-autoconnect=".Length..];
            }
            else if (a.StartsWith("--dcmp-playername=", StringComparison.Ordinal))
            {
                playerName = a["--dcmp-playername=".Length..];
            }
        }
    }

    public static void StartTest(int clients = 2, string ip = "127.0.0.1", int port = 44567) => HoldAsync(StartTestAsync(clients, ip, port));

    private static async Task StartTestAsync(int clients, string ip, int port)
    {
        DebugFlags.ClearViaAnsi();

        Logger.Information("Host starting on {f1}:{f2}", ip, port);

        await ClientRuntime.Instance.StartHost(ip, port);

        await Task.Delay(500);

        var gc = ClientRuntime.Instance.CurrentGuestClient;
        if (gc == null)
        {
            return;
        }

        gc.SetReady(true);

        await Task.Delay(500);
        for (var i = 0; i < clients; i++)
        {
            LaunchClient(ip, port, i);
        }

        Logger.Information("Launched {f1} clients", clients);

        await AutoStartWhenReady(clients, timeoutMs: 120_000);

    }

    private static void LaunchClient(string ip, int port, int idx)
    {
        var psi = new ProcessStartInfo
        {
            FileName = EXE_PATH,
            UseShellExecute = false,
        };

        psi.EnvironmentVariables["DCMP_AUTOCONNECT"] = $"{ip}:{port}";
        psi.EnvironmentVariables["DCMP_PLAYERNAME"] = $"TestPlayer{idx + 1}";

        psi.EnvironmentVariables.Remove("DCCM_MULTIPLAYER_NO_CLIENT");


        var p = Process.Start(psi);
        Logger.Information("Launched PID={f1} as TestPlayer{f2}", p?.Id, idx + 1);
    }


    private static async Task AutoStartWhenReady(int clientCount, int timeoutMs)
    {
        var expected = 1 + clientCount;
        var sw = Stopwatch.StartNew();

        var lastCount = -1;
        var lastReady = -1;

        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(500);

            var hc = ClientRuntime.Instance.CurrentHostClient;
            if (hc == null)
            {
                continue;
            }

            var guests = hc.LobbyInfo?.Guests;
            if (guests == null)
            {
                continue;
            }

            var count = guests.Count;
            var readyCount = 0;
            foreach (var kv in guests)
            {
                if (kv.Value.IsReady)
                {
                    readyCount++;
                }
            }

            if (count != lastCount || readyCount != lastReady)
            {
                lastCount = count;
                lastReady = readyCount;
                Logger.Information(
                    "{f1}/{f2} players, {f3} ready, " +
                    "CanStart={f4}", count, expected, readyCount, hc.CanStartGame);
            }
            if (count >= expected && readyCount >= expected && hc.CanStartGame)
            {
                Logger.Information("{Source} All ready, starting game...", nameof(DebugLauncher));
                await hc.StartGame();
                return;
            }
        }
    }

    public static void TriggerAutoJoin()
    {
        if (!IsTestClient)
        {
            return;
        }

        var parts = autoAddr!.Split(':');
        if (parts.Length < 2)
        {
            return;
        }

        var ip = parts[0];
        var port = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);

        Logger.Information(
            "Auto-joining {f1}:{f2} as {f3}", ip, port, playerName);

        autoAddr = null;

        HoldAsync(JoinAndReadyAsync(ip, port));
    }

    private static async Task JoinAndReadyAsync(string ip, int port)
    {
        Logger.Information("Starting guest...");
        await ClientRuntime.Instance.StartGuest(ip, port);

        await Task.Delay(500);

        var gc = ClientRuntime.Instance.CurrentGuestClient;
        if (gc == null)
        {
            return;
        }

        await Task.Delay(2000);

        gc.SetReady(true);
    }


    private static async void HoldAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, " HoldAsync failed");
        }
    }
}
#endif
