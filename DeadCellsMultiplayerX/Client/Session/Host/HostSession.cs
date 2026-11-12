namespace DeadCellsMultiplayerX.Client.Session.Host;

using System.Diagnostics;
using System.IO.Pipes;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Foundation.Processes;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using Serilog;

internal sealed class HostSession(IServerProcessLauncher serverProcessLauncher) : Disposable, IDisposable
{
    private AnonymousPipeServerStream? inPipe;
    private AnonymousPipeServerStream? outPipe;

    private MultiplexingStream? multiplexingStream;
    private MultiplexingStream.Channel? mainChannel;
    private BinaryWriter? mainChannelWriter;
    private Process? serverProcess;
    private bool canJoin = true;

    private readonly ILogger serverLogger = Log.Logger.ForContext("SourceContext", "Server");

    private readonly IServerProcessLauncher serverProcessLauncher = serverProcessLauncher;

    public async Task Init()
    {
        await Task.Yield().ConfigureAwait(false); //不在主线程上执行

        this.Logger.Information("Starting server...");

        this.inPipe = new(PipeDirection.In, HandleInheritability.Inheritable);
        this.outPipe = new(PipeDirection.Out, HandleInheritability.Inheritable);

        this.serverProcess = this.serverProcessLauncher.Start(new ServerProcessOptions
        {
            EnvironmentVariables =
            {
                ["DCMP_HOST_IN_PIPE"] = this.inPipe.GetClientHandleAsString(),
                ["DCMP_HOST_OUT_PIPE"] = this.outPipe.GetClientHandleAsString(),
            },
            RedirectStandardOutput = true,
            RedirectStandardError = false,
        });

        this.serverProcess.Exited += this.ServerProcess_Exited;
        this.serverProcess.EnableRaisingEvents = true;
        //serverProcess.BeginErrorReadLine();
        this.serverProcess.BeginOutputReadLine();

        this.serverProcess.OutputDataReceived += this.ServerProcess_OutputDataReceived;
        //serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

        this.inPipe.DisposeLocalCopyOfClientHandle();
        this.outPipe.DisposeLocalCopyOfClientHandle();

        this.Logger.Information("Waiting server...");

        while (!this.inPipe.IsConnected)
        {
            this.DisposeToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        {
            var v = 0;
            while ((v = this.inPipe.ReadByte()) != 0x32)
            {
                this.DisposeToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }
        // await inPipe.ReadAtLeastAsync(new byte[1], 1, false); //等待服务器连接

        this.multiplexingStream = await MultiplexingStream.CreateAsync(
            FullDuplexStream.Splice(this.inPipe, this.outPipe),
            new MultiplexingStream.Options()
            {
                ProtocolMajorVersion = 3
            },
            this.DisposeToken
            );

        Debug.Assert(!((IDisposableObservable)this.multiplexingStream).IsDisposed);
        this.mainChannel = await this.multiplexingStream.OfferChannelAsync("main");
        this.mainChannelWriter = new(this.mainChannel.AsStream());

        this.Logger.Information("Server started");
    }

    private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {

    }

    private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e) => this.serverLogger.Information(e.Data ?? "");

    private void ServerProcess_Exited(object? sender, EventArgs e)
    {
        this.Logger.Information("Server process exited.");
        this.Dispose();
    }

    public void StartGame()
    {
        Debug.Assert(this.mainChannelWriter != null);

        this.canJoin = false; //不允许加入服务器
        this.mainChannelWriter.Write(-1); //告诉服务器开始游戏
    }

    public MultiplexingStream.Channel AllocNewChannel()
    {
        Debug.Assert(this.multiplexingStream != null);
        Debug.Assert(this.mainChannelWriter != null);

        if (!this.canJoin)
        {
            throw new InvalidOperationException();
        }

        var channel = this.multiplexingStream.CreateChannel();
        this.mainChannelWriter.Write((int)channel.QualifiedId.Id);
        return channel;
    }

    protected override void MyDispose()
    {
        this.DetachProcess();

        // Kill the worker first: the other end of the multiplexing stream
        // must be gone before we dispose it, otherwise DisposeAsync can wait
        // on a peer that is still writing.
        this.KillServerProcess();

        // Reverse creation order for the remaining managed resources.
        this.DisposeSafely(this.mainChannelWriter, "Host main channel writer");
        this.mainChannelWriter = null;
        this.DisposeSafely(this.mainChannel, "Host main channel");
        this.mainChannel = null;
        if (this.multiplexingStream != null)
        {
            // Dispose() is obsolete; block the sync fallback on the async API.
            try
            {
                DisposeAsyncBlocking(() => this.multiplexingStream.DisposeAsync());
            }
            catch (Exception ex)
            {
                this.Logger.Warning(ex, "Dispose failed: {Resource}", "Host multiplexing stream");
            }
            this.multiplexingStream = null;
        }

        this.DisposeSafely(this.inPipe, "Host in pipe");
        this.DisposeSafely(this.outPipe, "Host out pipe");
        this.inPipe = null;
        this.outPipe = null;
    }

    protected override async ValueTask MyDisposeAsync()
    {
        this.DetachProcess();

        // Same ordering rationale as the sync path.
        await this.KillServerProcessAsync().ConfigureAwait(false);

        this.DisposeSafely(this.mainChannelWriter, "Host main channel writer");
        this.mainChannelWriter = null;
        this.DisposeSafely(this.mainChannel, "Host main channel");
        this.mainChannel = null;
        if (this.multiplexingStream != null)
        {
            // The worker is already dead; cap the stream shutdown so a broken
            // peer cannot hold the host session forever.
            await this.DisposeSafelyAsync(
                () => new ValueTask(
                    this.multiplexingStream.DisposeAsync()
                        .AsTask()
                        .WaitAsync(TimeSpan.FromSeconds(2))),
                "Host multiplexing stream").ConfigureAwait(false);
            this.multiplexingStream = null;
        }

        this.DisposeSafely(this.inPipe, "Host in pipe");
        this.DisposeSafely(this.outPipe, "Host out pipe");
        this.inPipe = null;
        this.outPipe = null;
    }

    private void DetachProcess()
    {
        if (this.serverProcess == null)
        {
            return;
        }

        this.serverProcess.Exited -= this.ServerProcess_Exited;
        this.serverProcess.OutputDataReceived -= this.ServerProcess_OutputDataReceived;
    }

    private void KillServerProcess()
    {
        var process = this.serverProcess;
        if (process == null)
        {
            return;
        }

        this.serverProcess = null;
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
                if (!process.WaitForExit(2000))
                {
                    this.Logger.Warning(
                        "Server process did not exit within {TimeoutMs}ms",
                        2000);
                }
            }
        }
        catch (Exception ex)
        {
            // Best-effort: the worker may already be gone.
            this.Logger.Warning(ex, "Server process kill failed");
        }
        finally
        {
            process.Dispose();
        }
    }

    private async ValueTask KillServerProcessAsync()
    {
        var process = this.serverProcess;
        if (process == null)
        {
            return;
        }

        this.serverProcess = null;
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
                    this.Logger.Warning(
                        "Server process did not exit within {TimeoutMs}ms",
                        2000);
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Server process kill failed");
        }
        finally
        {
            process.Dispose();
        }
    }
}
