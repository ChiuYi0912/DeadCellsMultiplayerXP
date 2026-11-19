namespace DeadCellsMultiplayerX.Server.Session;

using DeadCellsMultiplayerX.Networking.Time;
using DeadCellsMultiplayerX.Server.Session.Connection;
using DeadCellsMultiplayerX.Server.Session.Interactions;
using DeadCellsMultiplayerX.Server.World;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using Nerdbank.Streams;
using Serilog;

internal sealed class ServerSession : IEventReceiver, IOnFrameUpdate,
    IDisposable, System.IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ServerTransport transport;
    private readonly ServerTimeSystem timeSystem = new();
    private readonly Dictionary<string, ServerGuestConnection> guests = [];
    // Keep ownership after roster removal until RPC dispatch and transport cleanup finish.
    private readonly HashSet<ServerGuestConnection> connections = [];
    private readonly Thread gameThread = Thread.CurrentThread;
    // Borrowed from ServerMain; the composition root owns disposal.
    private WorldCoordinator? worlds;
    private bool initializationStarted;
    private bool stopping;
    private bool eventsRegistered;

    public ILogger Logger { get; } = Log.ForContext<ServerSession>();
    public CancellationToken DisposeToken { get; }
    public bool IsDisposed => Volatile.Read(ref this.stopping);
    internal Task Completion => this.stopped.Task;
    public long CurrentTimeStamp => this.timeSystem.Now;

    internal SynchronizationContext GameContext { get; } =
        ModCore.Modules.Game.SynchronizationContext ?? throw new InvalidOperationException(
            "Game synchronization context is not available.");

    internal bool IsGameThread => Thread.CurrentThread == this.gameThread;

    internal void VerifyGameThread()
    {
        if (!this.IsGameThread)
        {
            throw new InvalidOperationException("Server game state must be accessed on the game thread.");
        }
    }

    public IReadOnlyCollection<string> ConnectedGuestIds
    {
        get
        {
            this.VerifyGameThread();
            return this.guests.Keys;
        }
    }

    public IReadOnlyCollection<ServerGuestConnection> ConnectedGuests
    {
        get
        {
            this.VerifyGameThread();
            return this.guests.Values;
        }
    }

    internal ServerLevelTransitionGateway LevelTransitions { get; private set; } = null!;
    public ServerSession()
    {
        this.DisposeToken = this.lifetime.Token;
        this.transport = new(this.Logger);
        EventSystem.AddReceiver(this);
        this.eventsRegistered = true;
    }

    /// <summary>Starts the Host transport and completes when initial guest admission ends.</summary>
    public Task Init(WorldCoordinator worlds)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.IsDisposed, this);
            if (this.initializationStarted)
            {
                if (!ReferenceEquals(this.worlds, worlds))
                {
                    throw new InvalidOperationException("Server session already has a world coordinator.");
                }

                return this.initialized.Task;
            }
            this.worlds = worlds;
            this.LevelTransitions = new ServerLevelTransitionGateway(this, worlds, this.Logger);
            this.initializationStarted = true;
        }
        _ = this.RunInitializationAsync();
        return this.initialized.Task;
    }

    private async Task RunInitializationAsync()
    {
        try
        {
            await this.GameContext;
            this.VerifyGameThread();
            this.DisposeToken.ThrowIfCancellationRequested();
            this.timeSystem.Start();
            await this.transport.RunAsync(this.AcceptGuestOnMainThreadAsync).ConfigureAwait(false);
            this.initialized.TrySetResult();
        }
        catch (Exception) when (this.IsDisposed)
        {
            this.initialized.TrySetCanceled(this.DisposeToken);
        }
        catch (Exception ex)
        {
            this.initialized.TrySetException(ex);
            this.Dispose();
        }
    }

    private async Task<bool> AcceptGuestOnMainThreadAsync(
        MultiplexingStream.Channel channel, Stream stream, string guestGuid)
    {
        await this.GameContext;
        this.VerifyGameThread();
        if (this.IsDisposed)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(guestGuid) || this.guests.ContainsKey(guestGuid))
        {
            this.Logger.Warning("Duplicate or empty guest GUID rejected: {Guid}", guestGuid);
            return false;
        }

        // The connection owns stream and channel from here, including Start failure.
        var guest = new ServerGuestConnection(this, this.worlds!, stream, channel, guestGuid);
        this.connections.Add(guest);
        try
        {
            this.guests.Add(guestGuid, guest);
            this.worlds!.CurrentWorld!.AddGuest(guestGuid, guest.RequestClientFrames);
            guest.Start();
            this.Logger.Information("Client Enter : {Guid}", guestGuid);
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Failed to start guest {Guid}", guestGuid);
            guest.Dispose();
        }
        return true;
    }

    public bool TryGetGuest(string guid, out ServerGuestConnection guest)
    {
        this.VerifyGameThread();
        return this.guests.TryGetValue(guid, out guest!);
    }

    public void RemoveGuest(ServerGuestConnection guest)
    {
        this.VerifyGameThread();
        if (!this.guests.TryGetValue(guest.ConnectionGuid, out var current) || !ReferenceEquals(current, guest))
        {
            return;
        }
        this.guests.Remove(guest.ConnectionGuid);
        this.Cleanup(() => this.worlds?.CurrentWorld?.RemoveGuest(guest.ConnectionGuid), "guest world state");
    }

    internal void ForgetStoppedConnection(ServerGuestConnection guest)
    {
        this.VerifyGameThread();
        this.connections.Remove(guest);
    }

    internal void NotifyInitialLevel(byte[] savedGame)
    {
        this.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        foreach (var guest in this.guests.Values.ToArray())
        {
            if (this.IsDisposed)
            {
                break;
            }

            guest.NotifyInitialLevel(savedGame);
        }
    }

    internal void NotifyNextLevel(string levelId)
    {
        this.VerifyGameThread();
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        foreach (var guest in this.guests.Values.ToArray())
        {
            if (this.IsDisposed)
            {
                break;
            }

            guest.NotifyNextLevel(levelId);
        }
    }

    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        if (this.IsDisposed)
        {
            return;
        }
        this.VerifyGameThread();
        this.timeSystem.Tick();
        try
        {
            var frame = this.worlds?.CurrentWorld?.Tick(dt, this.ConnectedGuestIds);
            if (frame != null)
            {
                foreach (var guest in this.guests.Values.ToArray())
                {
                    guest.PushWorldFrame(frame);
                }
            }
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "World frame tick failed");
        }
    }

    // Synchronous callers request shutdown; asynchronous callers join the same completion.
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
            if (!this.initializationStarted)
            {
                this.initialized.TrySetCanceled();
            }
        }
        this.Cleanup(this.lifetime.Cancel, "session cancellation");
        this.transport.StopAccepting();
        _ = this.FinishStopAsync();
        return this.stopped.Task;
    }

    private async Task FinishStopAsync()
    {
        try
        {
            await this.GameContext;
            this.VerifyGameThread();
            var closing = this.connections.ToArray();
            foreach (var guest in closing)
            {
                guest.Dispose();
            }

            await this.ObserveAsync(this.initialized.Task, "initialization");
            // Session cancellation already canceled the coordinator's linked lifetime.
            // Join world producers before completing session shutdown.
            if (this.worlds != null)
            {
                await this.worlds.CompleteOperationsAsync();
            }

            await Task.WhenAll(closing.Select(guest => guest.StopAsync()));

            await this.GameContext;
            this.VerifyGameThread();
            if (this.eventsRegistered)
            {
                this.Cleanup(() => EventSystem.RemoveReceiver(this), "session game events");
                this.eventsRegistered = false;
            }
            this.guests.Clear();
            this.worlds = null;
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "Server session shutdown failed");
        }
        finally
        {
            await this.ObserveAsync(this.transport.DisposeAsync().AsTask(), "Host transport shutdown");
            this.Cleanup(this.lifetime.Dispose, "session cancellation source");
            this.stopped.TrySetResult();
        }
    }

    private async Task ObserveAsync(Task task, string operation)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.Logger.Warning(ex, "Server {Operation} ended during shutdown", operation); }
    }

    private void Cleanup(Action cleanup, string resource)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex) { this.Logger.Warning(ex, "Server cleanup failed: {Resource}", resource); }
    }
}
