namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Networking.Transport;
using Nerdbank.Streams;
using Serilog;
using StreamJsonRpc;

/// <summary>Owns one RPC endpoint, its multiplexed transport and its trace listeners.</summary>
internal sealed class GuestRpcChannel : IDisposable, IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly Stream stream;
    private readonly MultiplexingStream multiplexingStream;
    private readonly TraceSource traceSource;
    private readonly ILogger logger;
    private TaskCompletionSource? stopped;

    public GuestRpcChannel(Stream stream, string role, ILogger logger)
    {
        this.stream = stream;
        this.logger = logger;
        this.Rpc = stream.CreateJsonRpc(role,
            NetLogSession.CreateJsonRpcFailureListener(role),
            out this.multiplexingStream, out this.traceSource);
    }

    public JsonRpc Rpc { get; }

    public void Dispose() => _ = this.StopAsync();

    public ValueTask DisposeAsync() => new(this.StopAsync());

    private Task StopAsync()
    {
        TaskCompletionSource completion;
        lock (this.gate)
        {
            if (this.stopped != null)
            {
                return this.stopped.Task;
            }
            completion = this.stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        // Mark the mux stopping before closing its input. Otherwise channel
        // termination can enqueue writes against an already-closed transport.
        var muxStop = this.multiplexingStream.DisposeAsync().AsTask();
        try
        {
            this.Rpc.Dispose();
        }
        catch (Exception ex) { this.logger.Warning(ex, "Guest RPC close failed"); }

        try
        {
            this.stream.Dispose();
        }
        catch (Exception ex) { this.logger.Warning(ex, "Guest RPC input close failed"); }

        _ = this.FinishStopAsync(muxStop, completion);

        return completion.Task;
    }

    private async Task FinishStopAsync(Task muxStop, TaskCompletionSource completion)
    {
        try
        {
            try
            {
                await muxStop.ConfigureAwait(false);
            }
            catch (Exception ex) { this.logger.Warning(ex, "Guest RPC mux close failed"); }
            // DisposeAsync may return while another mux failure is completing
            // disposal. Completion carries the background read/write failure.
            try
            {
                await this.multiplexingStream.Completion.ConfigureAwait(false);
            }
            catch (Exception ex) { this.logger.Debug(ex, "Guest RPC transport ended during shutdown"); }

            try
            {
                await this.Rpc.DispatchCompletion.ConfigureAwait(false);
            }
            catch (Exception ex) { this.logger.Warning(ex, "Guest RPC dispatch shutdown failed"); }
        }
        finally
        {
            try
            {
                this.traceSource.Close();
            }
            catch (Exception ex) { this.logger.Warning(ex, "Guest RPC trace close failed"); }

            completion.TrySetResult();
        }
    }
}
