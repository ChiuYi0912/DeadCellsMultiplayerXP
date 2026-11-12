namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using StreamJsonRpc;
using Save = dc.tool.Save;

internal sealed class GuestClient(BaseNetworkConnection connection) : ClientBase
{
    private const string LOBBY_RPC_ROLE = "guest-lobby";
    private const int LOBBY_LOOP_START_DELAY_MS = 1;
    private const int LOBBY_POLL_INTERVAL_MS = 500;

    private JsonRpc? rpc;

    public LobbyInfo? LobbyInfo { get; private set; }

    public string Guid { get; private set; } = string.Empty;

    public bool IsServerDrivenTransition => this.Session?.ServerDrivenTransition == true;

    internal GuestClientSession? Session { get; private set; }

    [AllowNull]
    private IHostClientRPC Host { get => field ?? throw new InvalidOperationException("The guest lobby RPC is not initialized."); set; }

    private bool IsHostReachable => !this.IsDisposed && this.rpc is { IsDisposed: false };

    public Task Init(string name)
    {
        var task = this.InitializeAsync(name);
        _ = task.ContinueWith(
            this.OnInitFaulted,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return task;
    }

    private async Task InitializeAsync(string name)
    {
        var createdRpc = connection.Stream.CreateJsonRpc(
            LOBBY_RPC_ROLE,
            NetLogSession.CreateJsonRpcFailureListener(LOBBY_RPC_ROLE));
        this.rpc = createdRpc;

        var createdHost = createdRpc.Attach<IHostClientRPC>();
        this.Host = createdHost;

        createdRpc.Disconnected += this.Rpc_Disconnected;
        createdRpc.StartListening();

        if (!await createdHost.CheckVersion(VersionUtils.ModVersion.ToString()))
        {
            this.Logger.Information("Failed to connect lobby: version mismatch.");
            this.Dispose();
            return;
        }

        this.Guid = await createdHost.GetGUID();

        this.SetName(name);

        var skinMould = this.ResolveLocalSkinMould();
        if (skinMould is not null)
        {
            await this.SetSkinMould(skinMould);
        }

        this.SetReady(false);

        this.LobbyInfo = await createdHost.GetLobbyInfo();

        this.StartLobbyLoop(createdHost);
    }

    private void OnInitFaulted(Task task)
    {
        this.Logger.Error(task.Exception, "Guest lobby initialization failed.");
        this.Dispose();
    }

    private void StartLobbyLoop(IHostClientRPC host) => _ = this.RunLobbyLoopAsync(host, this.DisposeToken)
            .ContinueWith(
                this.OnLobbyLoopCompleted,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

    private async Task RunLobbyLoopAsync(
        IHostClientRPC host,
        CancellationToken cancellationToken)
    {
        await Task.Delay(LOBBY_LOOP_START_DELAY_MS, cancellationToken)
            .ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested && this.Session is null)
        {
            var lobby = await host.GetLobbyInfo().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            this.LobbyInfo = lobby;

            if (lobby.CanConnectServer)
            {
                var serverStream = await host.GetServerStream()
                    .ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    this.DisposeSafely(serverStream, "Guest server stream");
                    return;
                }

                var newSession = new GuestClientSession(this, serverStream);
                this.Session = newSession;
                await newSession.Init().ConfigureAwait(false);
                return;
            }

            await Task.Delay(LOBBY_POLL_INTERVAL_MS, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void OnLobbyLoopCompleted(Task task)
    {
        if (this.IsDisposed)
        {
            this.DisposeSession();
            return;
        }

        if (task.IsFaulted)
        {
            this.Logger.Error(task.Exception, "Guest lobby loop failed.");
            this.Dispose();
        }
    }

    private void Rpc_Disconnected(
        object? sender,
        JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed)
        {
            return;
        }

        this.Logger.Error(
            e.Exception,
            "Abort connection: {reason}: {desc}",
            e.Reason,
            e.Description);

        this.Dispose();
    }

    protected override void MyDispose()
    {
        base.MyDispose();

        this.DisposeSession();
        this.DisposeLobbyRpc();

        this.DisposeSafely(connection, "Guest QUIC connection");
    }

    protected override async ValueTask MyDisposeAsync()
    {
        base.MyDispose();

        var sessionToDispose = this.Session;
        this.Session = null;

        if (sessionToDispose is not null)
        {
            await this.DisposeSafelyAsync(
                sessionToDispose.DisposeAsync,
                "Guest server session").ConfigureAwait(false);
        }

        this.DisposeLobbyRpc();

        await this.DisposeSafelyAsync(
            connection.DisposeAsync,
            "Guest QUIC connection").ConfigureAwait(false);
    }

    private void DisposeSession()
    {
        var sessionToDispose = this.Session;
        this.Session = null;
        this.DisposeSafely(sessionToDispose, "Guest server session");
    }

    private void DisposeLobbyRpc()
    {
        var rpcToDispose = this.rpc;
        this.rpc = null;
        this.Host = null;
        this.DisposeSafely(rpcToDispose, "Guest lobby RPC");
    }

    private string? ResolveLocalSkinMould()
    {
        var save = Save.Class.tryLoad();
        if (save is null)
        {
            this.Logger.Warning(
                "Local save data is unavailable; skin mould sync skipped.");
            return null;
        }

        return save.heroSkin.ToString();
    }

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

    public async Task RefreshLobbyInfo() => this.LobbyInfo = await this.Host.GetLobbyInfo();

    public async Task SelectNextLevel(string levelId)
    {
        var current = this.Session ?? throw new InvalidOperationException(
            "The guest server session is not initialized.");

        await current.Server.GusetEnterNextLevel(
            current.Client.Guid, levelId);
    }

    public void Quit()
    {
        if (this.IsHostReachable)
        {
            this.Host.Quit();
        }

        this.Dispose();
    }
}
