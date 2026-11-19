namespace DeadCellsMultiplayerX.Server.Session.Connection;

using System.Diagnostics;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Server.Integration.Events;
using DeadCellsMultiplayerX.Server.World;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Modules;
using Nerdbank.Streams;
using Serilog;
using StreamJsonRpc;

/// <summary>Owns one guest's identity, RPC, inner mux and accepted outer channel.</summary>
internal sealed partial class ServerGuestConnection : IDisposable, System.IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stream stream;
    private readonly MultiplexingStream.Channel channel;
    private readonly WorldCoordinator worlds;
    private readonly List<Task> nextLevelNotifications = [];
    private JsonRpc? rpc;
    private MultiplexingStream? rpcMux;
    private TraceSource? traceSource;
    private Task? versionDisconnectTask;
    private Task? initialLevelNotification;
    private bool stopping;

    public ILogger Logger { get; }
    public CancellationToken DisposeToken { get; }
    public bool IsDisposed => Volatile.Read(ref this.stopping);
    public string ConnectionGuid { get; }
    public GuestInfo GuestInfo { get; set; }
    public ServerSession Session { get; }
    public IGuestRPC Guest { get; private set; } = null!;

    public ServerGuestConnection(ServerSession session, WorldCoordinator worlds, Stream stream,
        MultiplexingStream.Channel channel, string connectionGuid)
    {
        session.VerifyGameThread();
        this.Session = session;
        this.worlds = worlds;
        this.stream = stream;
        this.channel = channel;
        this.ConnectionGuid = connectionGuid;
        this.GuestInfo = new GuestInfo { Guid = connectionGuid };
        this.DisposeToken = this.lifetime.Token;
        this.Logger = Log.ForContext("SourceContext", "Server-Guest-" + connectionGuid);
    }

    /// <summary>The session publishes this owner before listening can dispatch inbound RPC.</summary>
    internal void Start()
    {
        this.Session.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.IsDisposed || this.Session.IsDisposed, this);
        if (this.rpc != null)
        {
            throw new InvalidOperationException("Guest RPC is already initialized.");
        }

        var rpc = this.stream.CreateJsonRpc("server-guest", NetLogSession.CreateJsonRpcFailureListener("server-guest"),
            out var mux, out var trace);
        this.rpc = rpc;
        this.rpcMux = mux;
        this.traceSource = trace;
        rpc.AddLocalRpcTarget(this, new JsonRpcTargetOptions { DisposeOnDisconnect = false });
        this.Guest = rpc.Attach<IGuestRPC>();
        rpc.SynchronizationContext = this.Session.GameContext;
        rpc.Disconnected += this.Rpc_Disconnected;
        ObjectDisposedException.ThrowIf(this.IsDisposed || this.Session.IsDisposed, this);
        rpc.StartListening();
        this.Logger.Information("Connected.");
    }

    private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed || this.IsDisposed)
        {
            return;
        }

        this.Logger.Error(e.Exception, "Abort connection: {Reason}: {Description}", e.Reason, e.Description);
        // Never await DispatchCompletion from inside an RPC callback.
        this.Dispose();
    }

    // RPC callbacks must return before dispatch can finish; only owners await shutdown.
    public void Dispose() => _ = this.StopAsync();
    public ValueTask DisposeAsync() => new(this.StopAsync());

    internal Task StopAsync()
    {
        lock (this.gate)
        {
            if (this.stopping)
            {
                return this.stopped.Task;
            }

            this.stopping = true;
        }
        this.Cleanup(this.lifetime.Cancel, "connection cancellation");
        Task muxStop;
        try
        { muxStop = this.rpcMux?.DisposeAsync().AsTask() ?? Task.CompletedTask; }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest RPC mux close failed");
            muxStop = Task.CompletedTask;
        }
        this.Cleanup(() => this.rpc?.Dispose(), "guest RPC close");
        this.Cleanup(this.stream.Dispose, "guest stream");
        this.Cleanup(this.channel.Dispose, "guest outer channel");
        _ = this.FinishStopAsync(muxStop);
        return this.stopped.Task;
    }

    private async Task FinishStopAsync(Task muxStop)
    {
        try
        {
            var rpc = this.rpc;
            if (rpc != null)
            {
                await this.ObserveAsync(rpc.Completion, "RPC listener").ConfigureAwait(false);
                await this.ObserveAsync(rpc.DispatchCompletion, "RPC dispatch").ConfigureAwait(false);
            }
            await this.Session.GameContext;
            this.Session.VerifyGameThread();
            var versionRejection = this.versionDisconnectTask;
            var initialNotification = this.initialLevelNotification;
            var nextNotifications = this.nextLevelNotifications.ToArray();
            if (versionRejection != null)
            {
                await this.ObserveAsync(versionRejection, "version rejection").ConfigureAwait(false);
            }

            if (initialNotification != null)
            {
                await this.ObserveAsync(initialNotification, "initial level notification").ConfigureAwait(false);
            }

            foreach (var notification in nextNotifications)
            {
                await this.ObserveAsync(notification, "next level notification").ConfigureAwait(false);
            }

            await this.Session.GameContext;
            this.Session.VerifyGameThread();
            this.Cleanup(() => this.Session.RemoveGuest(this), "guest roster and ghost");
            await this.ObserveAsync(muxStop, "RPC mux shutdown").ConfigureAwait(false);
            if (this.rpcMux != null)
            {
                await this.ObserveAsync(this.rpcMux.Completion, "RPC mux completion").ConfigureAwait(false);
            }

            await this.Session.GameContext;
            this.Session.ForgetStoppedConnection(this);
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest connection shutdown failed");
        }
        finally
        {
            this.rpc?.Disconnected -= this.Rpc_Disconnected;
            this.Cleanup(() => this.traceSource?.Close(), "RPC trace");
            this.Cleanup(this.lifetime.Dispose, "connection cancellation source");
            this.stopped.TrySetResult();
        }
    }

    public void PushWorldFrame(WorldFrame frame)
    {
        if (this.IsDisposed || this.rpc?.IsDisposed != false)
        {
            return;
        }

        try
        {
            _ = this.Guest.SyncWorldFrame(frame).ContinueWith(
                task => this.Logger.Warning(task.Exception, "SyncWorldFrame failed"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex) { this.Logger.Warning(ex, "SyncWorldFrame failed (sync)"); }
    }

    public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
        => this.Guest.RequestClientFrames(request);

    internal void NotifyInitialLevel(byte[] data)
    {
        if (this.IsDisposed || this.Session.IsDisposed)
        {
            return;
        }

        this.Session.VerifyGameThread();
        this.initialLevelNotification ??= this.NotifyInitialLevelAsync(data);
        EventSystem.BroadcastEvent<IOnHeroInitDone, Hero>(Game.Instance.HeroInstance!);
    }

    private async Task NotifyInitialLevelAsync(byte[] data)
    {
        try
        {
            await this.Guest.EnterNewLevel(data).WaitAsync(this.DisposeToken);
        }
        catch (OperationCanceledException) when (this.IsDisposed) { }
        catch (Exception ex) { this.Logger.Warning(ex, "Initial level notification failed"); }
    }

    internal void NotifyNextLevel(string levelId)
    {
        if (this.IsDisposed || this.Session.IsDisposed)
        {
            return;
        }

        this.Session.VerifyGameThread();
        this.nextLevelNotifications.RemoveAll(task => task.IsCompleted);
        this.nextLevelNotifications.Add(this.NotifyNextLevelAsync(levelId));
    }

    private async Task NotifyNextLevelAsync(string levelId)
    {
        try
        {
            await this.Guest.EnterNextLevel(levelId).WaitAsync(this.DisposeToken);
        }
        catch (OperationCanceledException) when (this.IsDisposed) { }
        catch (Exception ex) { this.Logger.Warning(ex, "EnterNextLevel RPC failed: {Guid}", this.ConnectionGuid); }
    }

    private async Task ObserveAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.Logger.Debug(ex, "Guest {Operation} ended during shutdown", operation); }
    }

    private void Cleanup(Action cleanup, string resource)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex) { this.Logger.Warning(ex, "Guest cleanup failed: {Resource}", resource); }
    }
}
