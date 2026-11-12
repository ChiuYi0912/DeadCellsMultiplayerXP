using DeadCellsMultiplayerX.Networking.Time;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    /// <summary>
    /// Owns the guest's NTP sampling and server-time offset.  It has no game
    /// object references and no RPC plumbing of its own; the session supplies a
    /// read-only clock query and its disposed flag.
    /// </summary>
    internal sealed class GuestClockSync
    {
        private const long NTP_INTERVAL_MS = 500;

        private readonly ClientTimeSystem timeSystem = new();
        private readonly Func<bool> isDisposed;
        private readonly Func<bool> canRequest;
        private readonly Func<Task<long>> requestServerTime;
        private readonly ILogger logger;

        private Task? ntpTask;
        private long lastNtpRequestMs;
        private long lastServerFrameTimeMs = long.MinValue;

        public GuestClockSync(
            Func<bool> isDisposed,
            Func<bool> canRequest,
            Func<Task<long>> requestServerTime,
            ILogger logger)
        {
            this.isDisposed = isDisposed
                ?? throw new ArgumentNullException(nameof(isDisposed));
            this.canRequest = canRequest
                ?? throw new ArgumentNullException(nameof(canRequest));
            this.requestServerTime = requestServerTime
                ?? throw new ArgumentNullException(nameof(requestServerTime));
            this.logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Ready => timeSystem.Ready;

        public long SyncedTimeMs => timeSystem.SyncedTime;

        public long RttMs => timeSystem.RttMs;

        public long OffsetMs => timeSystem.OffsetMs;

        /// <summary>Called once per game frame.</summary>
        public void Tick()
        {
            if (timeSystem.Ready || !canRequest())
                return;

            long now = ClientTimeSystem.NowMs();
            if (now - lastNtpRequestMs < NTP_INTERVAL_MS)
                return;
            if (ntpTask != null && !ntpTask.IsCompleted)
                return;

            lastNtpRequestMs = now;
            ntpTask = SampleAsync();
        }

        /// <summary>Called for every received world frame.</summary>
        public void ObserveServerTime(long serverTimeMs)
        {
            if (serverTimeMs <= lastServerFrameTimeMs)
                return;

            lastServerFrameTimeMs = serverTimeMs;
            timeSystem.OnSnapshot(serverTimeMs);
        }

        private async Task SampleAsync()
        {
            try
            {
                long t0 = ClientTimeSystem.NowMs();
                long serverTime = await requestServerTime().ConfigureAwait(false);
                long t1 = ClientTimeSystem.NowMs();

                timeSystem.AddSample(t0, t1, serverTime);
            }
            catch (Exception) when (isDisposed())
            {
                // Session is shutting down; ignore the in-flight sample.
            }
            catch (Exception ex)
            {
                logger.Error(ex, "NTP sample failed");
            }
        }
    }
}
