namespace DeadCellsMultiplayerX.Client.Session.Guest;

internal sealed partial class GuestClientSession
{
    private GuestClockSync Clock
        => field ??= new GuestClockSync(
            () => this.IsDisposed,
            () => this.server != null,
            this.Server.GetTimeStamp,
            this.Logger);

    /// <summary>
    /// 与服务端同步的时间轴
    /// </summary>
    public long SyncedTimeMs => this.Clock.SyncedTimeMs;

    /// <summary>
    /// 最近一批 NTP 采样的最优 RTT（毫秒），未完成采样时为 -1
    /// </summary>
    public long ServerRttMs => this.Clock.RttMs;

    /// <summary>
    /// 本地时钟相对服务端时钟的偏移（毫秒）
    /// </summary>
    public long ServerClockOffsetMs => this.Clock.OffsetMs;
}
