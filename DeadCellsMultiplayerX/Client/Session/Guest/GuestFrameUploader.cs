using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    /// <summary>
    /// Owns the local frame capture buffer, upload backpressure and upload-failure
    /// state.  The session supplies the RPC delegate and identity; this class does
    /// not know about hooks, world state or timing.
    /// </summary>
    internal sealed class GuestFrameUploader
    {
        private readonly ClientFrameSendBuffer sendBuffer = new();
        private readonly Func<bool> isDisposed;
        private readonly Func<string> guid;
        private readonly Func<long> syncedTimeMs;
        private readonly Func<ClientWorldFrame, Task> upload;
        private readonly ILogger logger;

        private Task? uploadTask;
        private long lastUploadErrorLogMs;
        private bool uploadBroken;

        public GuestFrameUploader(
            Func<bool> isDisposed,
            Func<string> guid,
            Func<long> syncedTimeMs,
            Func<ClientWorldFrame, Task> upload,
            ILogger logger)
        {
            this.isDisposed = isDisposed
                ?? throw new ArgumentNullException(nameof(isDisposed));
            this.guid = guid
                ?? throw new ArgumentNullException(nameof(guid));
            this.syncedTimeMs = syncedTimeMs
                ?? throw new ArgumentNullException(nameof(syncedTimeMs));
            this.upload = upload
                ?? throw new ArgumentNullException(nameof(upload));
            this.logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool UploadBroken => uploadBroken;

        /// <summary>Captures the local hero and sends at most one in-flight frame.</summary>
        public void CaptureAndUpload(Hero hero)
        {
            if (isDisposed() || uploadBroken)
                return;

            var game = Game.Class.ME;
            if (hero != game?.hero || hero.destroyed)
                return;

            if (!sendBuffer.Capture(hero, guid(), syncedTimeMs()))
                return;

            if (uploadTask != null && !uploadTask.IsCompleted)
                return;

            var frame = sendBuffer.BuildFrameAndRecord();
            if (frame == null)
                return;

#if DEBUG
            if (WorldFrameDebug.ConsumeSkipClientUpload())
                return;
#endif

            try
            {
                uploadTask = upload(frame);
                _ = uploadTask.ContinueWith(
                    OnUploadFaulted,
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                HandleUploadFailure(ex);
            }
        }

        public ClientFrameBundle BuildBundle(ClientFrameRangeRequest request)
            => sendBuffer.BuildBundle(request);

        public void SetEpoch(int epoch) => sendBuffer.SetEpoch(epoch);

        public void ResetLocalHeroState() => sendBuffer.ResetLocalHeroState();

        public void Clear() => sendBuffer.Clear();

        private void OnUploadFaulted(Task task)
            => HandleUploadFailure(task.Exception?.GetBaseException());

        private void HandleUploadFailure(Exception? ex)
        {
            if (isDisposed())
                return;

            // 连接已经断开时,等待 Rpc_Disconnected 清理
            if (ex is StreamJsonRpc.ConnectionLostException
                or OperationCanceledException)
            {
                uploadBroken = true;
                return;
            }

            long now = Environment.TickCount64;
            if (now - lastUploadErrorLogMs < 1000)
                return;

            lastUploadErrorLogMs = now;
            logger.Warning(
                "UploadClientWorldFrame failed: {Message}",
                ex?.Message ?? "unknown");
        }
    }
}
