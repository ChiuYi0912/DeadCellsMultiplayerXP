namespace DeadCellsMultiplayerX.Server.Session;

using System.IO.Pipes;
using DeadCellsMultiplayerX.Networking.Transport;
using Microsoft.VisualStudio.Threading;
using Nerdbank.Streams;
using Serilog;

/// <summary>Owns the Host pipes, outer mux and channels until a guest connection accepts ownership.</summary>
internal sealed class ServerTransport(ILogger logger) : System.IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly ILogger logger = logger;
    private AnonymousPipeClientStream? outPipe;
    private AnonymousPipeClientStream? inPipe;
    private MultiplexingStream? mux;
    private MultiplexingStream.Channel? mainChannel;
    private Stream? mainReader;
    private Stream? pendingGuestStream;
    private Task? runTask;
    private TaskCompletionSource? stopped;
    private bool acceptingStopped;

    public Task RunAsync(Func<MultiplexingStream.Channel, Stream, string, Task<bool>> acceptGuest)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.acceptingStopped, this);
            return this.runTask ??= this.RunCoreAsync(acceptGuest);
        }
    }

    private async Task RunCoreAsync(Func<MultiplexingStream.Channel, Stream, string, Task<bool>> acceptGuest)
    {
        await Task.Yield().ConfigureAwait(false);
        var token = this.lifetime.Token;
        token.ThrowIfCancellationRequested();
        this.outPipe = new(PipeDirection.Out, Environment.GetEnvironmentVariable("DCMP_HOST_IN_PIPE")!);
        this.inPipe = new(PipeDirection.In, Environment.GetEnvironmentVariable("DCMP_HOST_OUT_PIPE")!);
        token.ThrowIfCancellationRequested();
        this.outPipe.WriteByte(0x32);

        this.logger.Information("Waiting host...");
        this.mux = await MultiplexingStream.CreateAsync(
            FullDuplexStream.Splice(this.inPipe, this.outPipe),
            new MultiplexingStream.Options { ProtocolMajorVersion = 3 }, token).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await Task.Delay(10, token).ConfigureAwait(false);

        this.logger.Information("Binding main channel...");
        this.mainChannel = await this.mux.AcceptChannelAsync("main", token).ConfigureAwait(false);
        this.mainReader = this.mainChannel.AsStream();
        this.logger.Information("Waiting guests...");

        var number = new byte[4];
        while (true)
        {
            await this.mainReader.ReadAtLeastAsync(number, 4, true, token).ConfigureAwait(false);
            var channelId = BitConverter.ToInt32(number);
            if (channelId == -1)
            {
                return;
            }

            this.logger.Information("Connecting from channel {Id}", channelId);
            var channel = this.mux.AcceptChannel(channelId);
            var stream = channel.AsStream();
            var transferred = false;
            try
            {
                lock (this.gate)
                {
                    token.ThrowIfCancellationRequested();
                    this.pendingGuestStream = stream;
                }
                var guid = await StreamUtils.ReadGuestGuidAsync(stream, token).ConfigureAwait(false);
                lock (this.gate)
                {
                    token.ThrowIfCancellationRequested();
                    // During registration only this operation owns the handoff. Session
                    // rejects it if stopping; StopAccepting must not close an accepted stream.
                    this.pendingGuestStream = null;
                }
                transferred = await acceptGuest(channel, stream, guid).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                this.logger.Warning(ex, "Failed to accept guest channel {Id}", channelId);
            }
            finally
            {
                lock (this.gate)
                {
                    if (ReferenceEquals(this.pendingGuestStream, stream))
                    {
                        this.pendingGuestStream = null;
                    }
                }
                if (!transferred)
                {
                    this.Cleanup(stream.Dispose, "unclaimed guest stream");
                    this.Cleanup(channel.Dispose, "unclaimed guest channel");
                }
            }
        }
    }

    /// <summary>Interrupts only admission; accepted guest channels remain alive until their owners stop.</summary>
    public void StopAccepting()
    {
        Stream? pending;
        Stream? reader;
        lock (this.gate)
        {
            if (this.acceptingStopped)
            {
                return;
            }
            this.acceptingStopped = true;
            pending = this.pendingGuestStream;
            reader = this.mainReader;
        }
        this.Cleanup(this.lifetime.Cancel, "admission cancellation");
        this.Cleanup(() => pending?.Dispose(), "pending guest handshake");
        this.Cleanup(() => reader?.Dispose(), "main channel read");
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        lock (this.gate)
        {
            if (this.stopped != null)
            {
                return new(this.stopped.Task);
            }
            completion = this.stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        this.StopAccepting();
        _ = this.FinishStopAsync(completion);
        return new(completion.Task);
    }

    private async Task FinishStopAsync(TaskCompletionSource completion)
    {
        try
        {
            var running = this.runTask;
            if (running != null)
            {
                await this.ObserveAsync(running, "admission loop").ConfigureAwait(false);
            }
            this.Cleanup(() => this.mainReader?.Dispose(), "main channel stream");
            this.Cleanup(() => this.mainChannel?.Dispose(), "main channel");
            if (this.mux != null)
            {
                try
                {
                    await this.mux.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex) { this.logger.Warning(ex, "Host mux shutdown failed"); }
                await this.ObserveAsync(this.mux.Completion, "Host mux completion").ConfigureAwait(false);
            }
        }
        finally
        {
            this.Cleanup(() => this.inPipe?.Dispose(), "Host input pipe");
            this.Cleanup(() => this.outPipe?.Dispose(), "Host output pipe");
            this.Cleanup(this.lifetime.Dispose, "admission cancellation source");
            completion.TrySetResult();
        }
    }

    private void Cleanup(Action action, string resource)
    {
        try
        { action(); }
        catch (Exception ex) { this.logger.Warning(ex, "Transport cleanup failed: {Resource}", resource); }
    }

    private async Task ObserveAsync(Task task, string operation)
    {
        try
        { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) when (this.acceptingStopped) { }
        catch (Exception ex) { this.logger.Debug(ex, "Transport operation ended: {Operation}", operation); }
    }
}
