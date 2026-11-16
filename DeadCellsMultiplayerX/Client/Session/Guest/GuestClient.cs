namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using Microsoft.VisualStudio.Threading;
using Serilog;
using StreamJsonRpc;
using Save = dc.tool.Save;

/// <summary>Owns the lobby connection and its optional game session.</summary>
internal sealed class GuestClient(BaseNetworkConnection connection, SynchronizationContext gameContext)
    : IDisposable, System.IAsyncDisposable
{
    // Fields
    private readonly Lock gate = new();
    private readonly BaseNetworkConnection connection = connection;
    private readonly SynchronizationContext gameContext = gameContext;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger logger = Log.ForContext<GuestClient>();

    private GuestLifetimeState state;
    private GuestRpcChannel? channel;
    private Task? lobbyLoop;
    private int stopNotified;

    // Events
    public event Action<GuestClient, bool>? Stopping;

    // Public properties
    public LobbyInfo? LobbyInfo { get; private set; }
    public string Guid { get; private set; } = string.Empty;
    internal GuestGameSession? Session { get; private set; }

    public GuestLifetimeState State
    {
        get
        {
            lock (this.gate)
            {
                return this.state;
            }
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (this.gate)
            {
                return this.state >= GuestLifetimeState.Stopping;
            }
        }
    }

    // Private properties
    [AllowNull]
    private IHostClientRPC Host
    {
        get => field ?? throw new InvalidOperationException(
            "Guest lobby RPC is not initialized.");
        set;
    }

    private bool IsHostReachable =>
        !this.IsDisposed && this.channel?.Rpc.IsDisposed == false;

    // Public methods
    public Task JoinLobbyAsync(string name, CancellationToken cancellationToken = default)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.state >= GuestLifetimeState.Stopping, this);

            if (this.state != GuestLifetimeState.Created)
            {
                return this.started.Task;
            }

            this.state = GuestLifetimeState.Starting;
        }

        _ = this.RunJoinAsync(name, cancellationToken);
        return this.started.Task;
    }

    public void Dispose() => _ = this.StopAsync();

    public ValueTask DisposeAsync() => new(this.StopAsync());

    public void SetName(string name) => this.Host.SetName(name);

    public void SetReady(bool ready) => this.Host.SetReady(ready);

    public Task SetSkinMould(string skinMould) => this.Host.SetSkinMould(skinMould);

    public async Task<long> Ping()
    {
        if (!this.IsHostReachable)
        {
            return -1;
        }

        var started = Stopwatch.GetTimestamp();
        await this.Host.Ping();
        return (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    public async Task RefreshLobbyInfo()
    {
        var lobby = await this.Host.GetLobbyInfo();

        if (!this.IsDisposed)
        {
            this.LobbyInfo = lobby;
        }
    }

    public Task SubmitLevelVote(string levelId)
        => (this.Session ?? throw new InvalidOperationException(
                "Guest game session is not initialized."))
            .SubmitLevelVote(levelId);

    public void Quit()
    {
        try
        {
            if (this.IsHostReachable)
            {
                this.Host.Quit();
            }
        }
        catch (Exception ex)
        {
            this.logger.Debug(ex, "Guest quit notification failed");
        }
        finally
        {
            this.RequestStop(false);
        }
    }

    // Private methods
    private async Task RunJoinAsync(string name, CancellationToken cancellationToken)
    {
        // UI cancellation applies to joining, not to the entire game lifetime.
        using var cancellation = cancellationToken.Register(this.Dispose);

        try
        {
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();

            this.channel = new GuestRpcChannel(this.connection.Stream, "guest-lobby", this.logger);
            var rpc = this.channel.Rpc;
            this.Host = rpc.Attach<IHostClientRPC>();
            rpc.Disconnected += this.OnDisconnected;
            rpc.StartListening();
            this.lifetime.Token.ThrowIfCancellationRequested();

            if (!await this.Host.CheckVersion(VersionUtils.ModVersion.ToString()))
            {
                throw new InvalidOperationException("Guest lobby version mismatch.");
            }

            this.lifetime.Token.ThrowIfCancellationRequested();
            this.Guid = await this.Host.GetGUID();
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();
            this.SetName(name);

            var skinMould = this.ResolveLocalSkinMould();
            if (skinMould != null)
            {
                await this.SetSkinMould(skinMould);
            }

            this.lifetime.Token.ThrowIfCancellationRequested();
            this.SetReady(false);
            var lobby = await this.Host.GetLobbyInfo();
            await this.gameContext;

            lock (this.gate)
            {
                if (this.state >= GuestLifetimeState.Stopping)
                {
                    throw new OperationCanceledException(this.lifetime.Token);
                }

                this.LobbyInfo = lobby;
                this.state = GuestLifetimeState.Active;
            }

            this.lobbyLoop = this.RunLobbyLoopAsync();
            this.started.TrySetResult();
        }
        catch (Exception ex)
        {
            if (this.lifetime.IsCancellationRequested || ex is OperationCanceledException)
            {
                this.started.TrySetCanceled(this.lifetime.Token);
            }
            else
            {
                this.started.TrySetException(ex);
            }

            if (!this.IsDisposed)
            {
                this.logger.Error(ex, "Guest lobby initialization failed");
                this.RequestStop(false);
            }
        }
    }

    private async Task RunLobbyLoopAsync()
    {
        try
        {
            await Task.Delay(1, this.lifetime.Token);

            while (true)
            {
                var lobby = await this.Host.GetLobbyInfo();
                await this.gameContext;
                this.lifetime.Token.ThrowIfCancellationRequested();
                this.LobbyInfo = lobby;

                if (lobby.CanConnectServer)
                {
                    // Await the actual resource-producing operation. WaitAsync(token)
                    // would abandon a late stream without an owner.
                    var stream = await this.Host.GetServerStream();
                    await this.gameContext;

                    lock (this.gate)
                    {
                        if (this.state >= GuestLifetimeState.Stopping)
                        {
                            stream.Dispose();
                            return;
                        }

                        try
                        {
                            this.Session = new GuestGameSession(
                                this.Guid,
                                lobby,
                                stream,
                                this.gameContext,
                                returnToMenu =>
                                {
                                    if (returnToMenu)
                                    {
                                        this.RequestStop(true);
                                    }
                                    else
                                    {
                                        this.Quit();
                                    }
                                });
                        }
                        catch
                        {
                            try
                            {
                                stream.Dispose();
                            }
                            catch (Exception ex)
                            {
                                this.logger.Warning(
                                    ex,
                                    "Unclaimed game stream cleanup failed");
                            }

                            throw;
                        }
                    }

                    await this.Session.StartAsync();
                    return;
                }

                await Task.Delay(500, this.lifetime.Token);
            }
        }
        catch (Exception ex)
        {
            if (this.IsDisposed)
            {
                return;
            }

            this.logger.Error(ex, "Guest lobby polling/game startup failed");
            this.RequestStop(true);
        }
    }

    private void OnDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed || this.IsDisposed)
        {
            return;
        }

        this.logger.Warning(
            e.Exception,
            "Guest lobby disconnected: {Reason}: {Description}",
            e.Reason,
            e.Description);

        this.RequestStop(this.Session != null);
    }

    private void RequestStop(bool returnToMenu)
    {
        this.Dispose();

        if (Interlocked.Exchange(ref this.stopNotified, 1) == 0)
        {
            this.Stopping?.Invoke(this, returnToMenu);
        }
    }

    private Task StopAsync()
    {
        lock (this.gate)
        {
            if (this.state >= GuestLifetimeState.Stopping)
            {
                return this.stopped.Task;
            }

            if (this.state == GuestLifetimeState.Created)
            {
                this.started.TrySetCanceled();
            }

            this.state = GuestLifetimeState.Stopping;
        }

        this.lifetime.Cancel();
        this.Session?.Dispose();
        this.channel?.Dispose();
        _ = this.FinishStopAsync();
        return this.stopped.Task;
    }

    private async Task FinishStopAsync()
    {
        try
        {
            try
            {
                await this.connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "Guest QUIC shutdown failed");
            }

            await this.ObserveShutdownAsync(this.started.Task);

            if (this.lobbyLoop != null)
            {
                await this.ObserveShutdownAsync(this.lobbyLoop);
            }

            // Stop again after joining startup to include resources published
            // concurrently with the original stop request.
            if (this.Session != null)
            {
                await this.Session.DisposeAsync();
            }

            if (this.channel != null)
            {
                await this.channel.DisposeAsync();
            }

            this.Session = null;
            this.channel = null;
            this.Host = null;
        }
        catch (Exception ex)
        {
            this.logger.Warning(ex, "Guest shutdown failed");
        }
        finally
        {
            this.lifetime.Dispose();

            lock (this.gate)
            {
                this.state = GuestLifetimeState.Stopped;
            }

            this.stopped.TrySetResult();
        }
    }

    private async Task ObserveShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.logger.Debug(ex, "Guest operation ended during shutdown");
        }
    }

    private string? ResolveLocalSkinMould()
    {
        var save = Save.Class.tryLoad();

        if (save is null)
        {
            this.logger.Warning(
                "Local save data is unavailable; skin mould sync skipped.");
            return null;
        }

        return save.heroSkin.ToString();
    }
}