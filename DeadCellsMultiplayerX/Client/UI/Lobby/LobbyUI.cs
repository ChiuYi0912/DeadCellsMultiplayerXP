namespace DeadCellsMultiplayerX.Client.UI.Lobby;

using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby.Controllers;
using DeadCellsMultiplayerX.Client.UI.Lobby.Views;

//入口
internal sealed class LobbyUI : UIProcess
{
    private readonly ClientRuntime client;
    private readonly LobbyStateMachine state = new();
    public event Action? OnCloseRequested;

    private readonly HostController host;
    private readonly GuestController guest;
    private readonly LobbyView view;

    public LobbyState State => this.state.Current;
    public LobbyContext Context { get; } = new();

    private double latencyTimer;
    private bool latencyInFlight;
    private const double LATENCY_INTERVAL_SEC = 2.0;

    public LobbyUI(ClientRuntime client, TitleScreen titleScreen, Process? parent = null)
        : base(parent)
    {
        this.client = client;

        this.host = new HostController(client, this.Context, this.state);
        this.guest = new GuestController(client, this.Context, this.state);
        this.view = new LobbyView(this, titleScreen, this);

        this.state.OnChanged += this.OnStateChanged;

        this.view.Show();
        this.state.ForceGo(LobbyState.MainMenu);

        titleScreen.controller.manualLock = true;
        titleScreen.blur(default, default);
    }

    public Task OnCreateRoom(string ip, int port) => this.host.CreateRoom(ip, port);
    public Task OnJoinRoom(string ip, int port) => this.guest.JoinRoom(ip, port);
    public Task OnStartGame() => this.host.StartGame();
    public void OnToggleReady() => this.guest.ToggleReady();

    public void OnRequestClose() => OnCloseRequested?.Invoke();

    public void OnLeaveRoom()
    {
        if (this.Context.IsHost)
        {
            this.host.DestroyRoom();
        }
        else
        {
            this.guest.LeaveRoom();
        }
    }

    public override void update()
    {
        base.update();
        if (this.IsDisposed)
        {
            return;
        }

        this.RefreshContext();
        this.TickLatency();
        this.view.Refresh(this.Context, this.State);
    }

    private void TickLatency()
    {
        if (this.latencyInFlight)
        {
            return;
        }

        this.latencyTimer += 1.0 / 60.0;
        if (this.latencyTimer < LATENCY_INTERVAL_SEC)
        {
            return;
        }

        this.latencyTimer = 0;

        var gc = this.client.CurrentGuestClient;
        if (gc == null)
        {
            return;
        }

        this.latencyInFlight = true;
        _ = this.PingAsync(gc);
    }

    private async Task PingAsync(GuestClient gc)
    {
        try
        {
            var ms = await gc.Ping();
            this.Context.LatencyMs = ms;
        }
        finally
        {
            this.latencyInFlight = false;
        }
    }

    private void RefreshContext()
    {
        var lobby = this.Context.IsHost
            ? this.client.CurrentHostClient?.LobbyInfo
            : this.client.CurrentGuestClient?.LobbyInfo;

        this.Context.Lobby = lobby;

        var gc = this.client.CurrentGuestClient;
        if (gc == null || lobby == null)
        {
            this.Context.Me = null;
            return;
        }

        this.Context.Me = lobby.Guests.TryGetValue(gc.Guid, out var me) ? me : null;
    }

    private void OnStateChanged(LobbyState from, LobbyState to)
    {
        this.Logger.Information($"LobbyState: {from} -> {to}");
        this.view.OnEnterState(to, this.Context);
    }

    public void OnGoCreateRoom() => this.state.ForceGo(LobbyState.CreateRoom);
    public void OnGoJoinRoom() => this.state.ForceGo(LobbyState.JoinRoom);
    public void OnBackToMain() => this.state.ForceGo(LobbyState.MainMenu);
    public void OnReturnToMain()
    {
        this.Context.Reset();
        this.state.ForceGo(LobbyState.MainMenu);
    }
    public void OnGotoDebug() => this.state.ForceGo(LobbyState.Debug);

    protected override void MyDispose()
    {
        this.state.OnChanged -= this.OnStateChanged;
        OnCloseRequested = null;

        this.host.Dispose();
        this.guest.Dispose();
        this.view.Dispose();
    }
}
