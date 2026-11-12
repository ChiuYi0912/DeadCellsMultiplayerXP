namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using Serilog;

/// <summary>
/// Owns the local frame capture buffer, upload backpressure and upload-failure
/// state.  The session supplies the RPC delegate and identity; this class does
/// not know about hooks, world state or timing.
/// </summary>
internal sealed class GuestFrameUploader(
    string guid,
    Func<long> syncedTimeMs,
    Func<ClientWorldFrame, Task> upload,
    ILogger logger)
{

    private readonly ClientFrameSendBuffer sendBuffer = new();

    private readonly string guid = guid
            ?? throw new ArgumentNullException(nameof(guid));
    private readonly Func<long> syncedTimeMs = syncedTimeMs
            ?? throw new ArgumentNullException(nameof(syncedTimeMs));
    private readonly Func<ClientWorldFrame, Task> upload = upload
            ?? throw new ArgumentNullException(nameof(upload));
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

    private Task? uploadTask;
    private long lastUploadErrorLogMs;
    private volatile bool disposed;
    private volatile bool ready;
    public bool UploadBroken { get; private set; }

    public void SetReady(bool value) => this.ready = value;
    public void MarkDisposed() => this.disposed = true;


    /// <summary>Captures the local hero and sends at most one in-flight frame.</summary>
    public void CaptureAndUpload(Hero hero)
    {
        if (!this.ready || this.disposed || this.UploadBroken)
        {
            return;
        }

        var game = Game.Class.ME;
        if (hero != game?.hero || hero.destroyed)
        {
            return;
        }

        if (!this.sendBuffer.Capture(hero, this.guid, this.syncedTimeMs()))
        {
            return;
        }

        if (this.uploadTask != null && !this.uploadTask.IsCompleted)
        {
            return;
        }

        var frame = this.sendBuffer.BuildFrameAndRecord();
        if (frame == null)
        {
            return;
        }

#if DEBUG
        if (WorldFrameDebug.ConsumeSkipClientUpload())
        {
            return;
        }
#endif

        this.uploadTask = this.RunUploadAsync(frame);
    }

    public ClientFrameBundle BuildBundle(ClientFrameRangeRequest request)
        => this.sendBuffer.BuildBundle(request);

    public void SetEpoch(int epoch) => this.sendBuffer.SetEpoch(epoch);
    public void ResetLocalHeroState() => this.sendBuffer.ResetLocalHeroState();

    public void Clear() => this.sendBuffer.Clear();

    private async Task RunUploadAsync(ClientWorldFrame frame)
    {
        try
        {
            await this.upload(frame).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.HandleUploadFailure(ex);
        }
    }

    private void HandleUploadFailure(Exception? ex)
    {
        if (this.disposed)
        {
            return;
        }

        if (ex is StreamJsonRpc.ConnectionLostException
            or OperationCanceledException)
        {
            this.UploadBroken = true;
            return;
        }

        var now = Environment.TickCount64;
        if (now - this.lastUploadErrorLogMs < 1000)
        {
            return;
        }

        this.lastUploadErrorLogMs = now;
        this.logger.Warning(
            "UploadClientWorldFrame failed: {Message}",
            ex?.Message ?? "unknown");
    }
}
