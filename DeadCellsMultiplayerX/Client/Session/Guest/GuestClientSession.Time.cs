namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    internal partial class GuestClientSession
    {
        private GuestClockSync? clock;

        private GuestClockSync Clock
            => clock ??= new GuestClockSync(
                () => IsDisposed,
                () => server != null,
                () => Server.GetTimeStamp(),
                Logger);

        /// <summary>
        /// 与服务端同步的时间轴
        /// </summary>
        public long SyncedTimeMs => Clock.SyncedTimeMs;

        /// <summary>
        /// 最近一批 NTP 采样的最优 RTT（毫秒），未完成采样时为 -1
        /// </summary>
        public long ServerRttMs => Clock.RttMs;

        /// <summary>
        /// 本地时钟相对服务端时钟的偏移（毫秒）
        /// </summary>
        public long ServerClockOffsetMs => Clock.OffsetMs;
    }
}
