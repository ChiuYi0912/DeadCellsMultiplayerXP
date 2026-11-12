namespace DeadCellsMultiplayerX.Client.Session.Guest;

using DeadCellsMultiplayerX.Networking.Time;
using Serilog;

/// <summary>
/// Owns the guest's NTP sampling and server-time offset.  It has no game
/// object references and no RPC plumbing of its own; the session supplies a
/// read-only clock query and its disposed flag.
/// </summary>
internal sealed class GuestClockSync(
    Func<bool> isDisposed,
    Func<bool> canRequest,
    Func<Task<long>> requestServerTime,
    ILogger logger)
{
    private const long NTP_INTERVAL_MS = 500;

    private readonly ClientTimeSystem timeSystem = new();
    private readonly Func<bool> isDisposed = isDisposed
            ?? throw new ArgumentNullException(nameof(isDisposed));
    private readonly Func<bool> canRequest = canRequest
            ?? throw new ArgumentNullException(nameof(canRequest));
    private readonly Func<Task<long>> requestServerTime = requestServerTime
            ?? throw new ArgumentNullException(nameof(requestServerTime));
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

    private Task? ntpTask;
    private long lastNtpRequestMs;
    private long lastServerFrameTimeMs = long.MinValue;

    public bool Ready => this.timeSystem.Ready;

    public long SyncedTimeMs => this.timeSystem.SyncedTime;

    public long RttMs => this.timeSystem.RttMs;

    public long OffsetMs => this.timeSystem.OffsetMs;

    /// <summary>Called once per game frame.</summary>
    public void Tick()
    {
        if (!this.canRequest() || this.timeSystem.Ready)
        {
            return;
        }

        var now = ClientTimeSystem.NowMs();
        if (now - this.lastNtpRequestMs < NTP_INTERVAL_MS)
        {
            return;
        }

        if (this.ntpTask != null && !this.ntpTask.IsCompleted)
        {
            return;
        }

        this.lastNtpRequestMs = now;
        this.ntpTask = this.SampleAsync();
    }

    /// <summary>Called for every received world frame.</summary>
    public void ObserveServerTime(long serverTimeMs)
    {
        if (serverTimeMs <= this.lastServerFrameTimeMs)
        {
            return;
        }

        this.lastServerFrameTimeMs = serverTimeMs;
        this.timeSystem.OnSnapshot(serverTimeMs);
    }

    private async Task SampleAsync()
    {
        try
        {
            var t0 = ClientTimeSystem.NowMs();
            var serverTime = await this.requestServerTime().ConfigureAwait(false);
            var t1 = ClientTimeSystem.NowMs();

            this.timeSystem.AddSample(t0, t1, serverTime);
        }
        catch (Exception) when (this.isDisposed())
        {
            // Session is shutting down; ignore the in-flight sample.
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "NTP sample failed");
        }
    }
}
