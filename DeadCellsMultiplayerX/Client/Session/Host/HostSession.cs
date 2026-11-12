using DeadCellsMultiplayerX.Foundation.Processes;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using Serilog;
using System.Diagnostics;
using System.IO.Pipes;

using DeadCellsMultiplayerX.Foundation.Lifecycle;
namespace DeadCellsMultiplayerX.Client.Session.Host
{
    internal class HostSession : Disposable, IDisposable
    {
        private AnonymousPipeServerStream? inPipe;
        private AnonymousPipeServerStream? outPipe;

        private MultiplexingStream? multiplexingStream;
        private MultiplexingStream.Channel? mainChannel;
        private BinaryWriter? mainChannelWriter;
        private Process? serverProcess;
        private bool canJoin = true;

        private ILogger serverLogger = Log.Logger.ForContext("SourceContext", "Server");

        private readonly IServerProcessLauncher serverProcessLauncher;

        public HostSession(IServerProcessLauncher serverProcessLauncher)
        {
            this.serverProcessLauncher = serverProcessLauncher;
        }

        public async Task Init()
        {
            await Task.Yield().ConfigureAwait(false); //不在主线程上执行

            Logger.Information("Starting server...");

            inPipe = new(PipeDirection.In, HandleInheritability.Inheritable);
            outPipe = new(PipeDirection.Out, HandleInheritability.Inheritable);

            serverProcess = serverProcessLauncher.Start(new ServerProcessOptions
            {
                EnvironmentVariables =
                {
                    ["DCMP_HOST_IN_PIPE"] = inPipe.GetClientHandleAsString(),
                    ["DCMP_HOST_OUT_PIPE"] = outPipe.GetClientHandleAsString(),
                },
                RedirectStandardOutput = true,
                RedirectStandardError = false,
            });

            serverProcess.Exited += ServerProcess_Exited;
            serverProcess.EnableRaisingEvents = true;
            //serverProcess.BeginErrorReadLine();
            serverProcess.BeginOutputReadLine();

            serverProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
            //serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

            inPipe.DisposeLocalCopyOfClientHandle();
            outPipe.DisposeLocalCopyOfClientHandle();

            Logger.Information("Waiting server...");

            while (!inPipe.IsConnected)
            {
                DisposeToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            {
                int v = 0;
                while ((v = inPipe.ReadByte()) != 0x32)
                {
                    DisposeToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
            // await inPipe.ReadAtLeastAsync(new byte[1], 1, false); //等待服务器连接

            multiplexingStream = await MultiplexingStream.CreateAsync(
                FullDuplexStream.Splice(inPipe, outPipe),
                new MultiplexingStream.Options()
                {
                    ProtocolMajorVersion = 3
                },
                DisposeToken
                );

            Debug.Assert(!((IDisposableObservable)multiplexingStream).IsDisposed);
            mainChannel = await multiplexingStream.OfferChannelAsync("main");
            mainChannelWriter = new(mainChannel.AsStream());

            Logger.Information("Server started");
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {

        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            serverLogger.Information(e.Data ?? "");
        }

        private void ServerProcess_Exited(object? sender, EventArgs e)
        {
            Logger.Information("Server process exited.");
            Dispose();
        }

        public void StartGame()
        {
            Debug.Assert(mainChannelWriter != null);

            canJoin = false; //不允许加入服务器
            mainChannelWriter.Write(-1); //告诉服务器开始游戏
        }

        public MultiplexingStream.Channel AllocNewChannel()
        {
            Debug.Assert(multiplexingStream != null);
            Debug.Assert(mainChannelWriter != null);

            if (!canJoin)
            {
                throw new InvalidOperationException();
            }

            var channel = multiplexingStream.CreateChannel();
            mainChannelWriter.Write((int)channel.QualifiedId.Id);
            return channel;
        }

        protected override void MyDispose()
        {
            DetachProcess();

            // Kill the worker first: the other end of the multiplexing stream
            // must be gone before we dispose it, otherwise DisposeAsync can wait
            // on a peer that is still writing.
            KillServerProcess();

            // Reverse creation order for the remaining managed resources.
            DisposeSafely(mainChannelWriter, "Host main channel writer");
            mainChannelWriter = null;
            DisposeSafely(mainChannel, "Host main channel");
            mainChannel = null;
            if (multiplexingStream != null)
            {
                // Dispose() is obsolete; block the sync fallback on the async API.
                try
                {
                    DisposeAsyncBlocking(() => multiplexingStream.DisposeAsync());
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Dispose failed: {Resource}", "Host multiplexing stream");
                }
                multiplexingStream = null;
            }

            DisposeSafely(inPipe, "Host in pipe");
            DisposeSafely(outPipe, "Host out pipe");
            inPipe = null;
            outPipe = null;
        }

        protected override async ValueTask MyDisposeAsync()
        {
            DetachProcess();

            // Same ordering rationale as the sync path.
            await KillServerProcessAsync().ConfigureAwait(false);

            DisposeSafely(mainChannelWriter, "Host main channel writer");
            mainChannelWriter = null;
            DisposeSafely(mainChannel, "Host main channel");
            mainChannel = null;
            if (multiplexingStream != null)
            {
                // The worker is already dead; cap the stream shutdown so a broken
                // peer cannot hold the host session forever.
                await DisposeSafelyAsync(
                    () => new ValueTask(
                        multiplexingStream.DisposeAsync()
                            .AsTask()
                            .WaitAsync(TimeSpan.FromSeconds(2))),
                    "Host multiplexing stream").ConfigureAwait(false);
                multiplexingStream = null;
            }

            DisposeSafely(inPipe, "Host in pipe");
            DisposeSafely(outPipe, "Host out pipe");
            inPipe = null;
            outPipe = null;
        }

        private void DetachProcess()
        {
            if (serverProcess == null)
                return;

            serverProcess.Exited -= ServerProcess_Exited;
            serverProcess.OutputDataReceived -= ServerProcess_OutputDataReceived;
        }

        private void KillServerProcess()
        {
            var process = serverProcess;
            if (process == null)
                return;

            serverProcess = null;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    if (!process.WaitForExit(2000))
                    {
                        Logger.Warning(
                            "Server process did not exit within {TimeoutMs}ms",
                            2000);
                    }
                }
            }
            catch (Exception ex)
            {
                // Best-effort: the worker may already be gone.
                Logger.Warning(ex, "Server process kill failed");
            }
            finally
            {
                process.Dispose();
            }
        }

        private async ValueTask KillServerProcessAsync()
        {
            var process = serverProcess;
            if (process == null)
                return;

            serverProcess = null;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    using var timeout = new CancellationTokenSource(
                        TimeSpan.FromSeconds(2));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        Logger.Warning(
                            "Server process did not exit within {TimeoutMs}ms",
                            2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Server process kill failed");
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
