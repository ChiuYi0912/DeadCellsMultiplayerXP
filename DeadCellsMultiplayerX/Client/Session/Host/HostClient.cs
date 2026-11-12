namespace DeadCellsMultiplayerX.Client.Session.Host;

using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Foundation.Processes;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Utilities.Threading;
using Microsoft.VisualStudio.Threading;

internal sealed class HostClient(
    BaseNetworkListener listener,
    CancellationToken cancellationToken,
    IServerProcessLauncher serverProcessLauncher) : ClientBase,
    IOnGuestQuit
{
    private readonly List<GuestConnection> guests = [];

    private readonly CancellationTokenSource acceptGuestsCancelSource = new();

    public HostSession? Session { get; set; }

    /// <summary>
    /// 房间信息
    /// </summary>
    public LobbyInfo LobbyInfo { get; set; } = new();


    /// <summary>
    /// 是否可以开始游戏
    /// </summary>
    public bool CanStartGame => !this.LobbyInfo.IsStarted && !this.LobbyInfo.CanConnectServer && this.LobbyInfo.Guests.Values.All(x => x.IsReady);

    /// <summary>
    /// 开始游戏
    /// 
    /// 在指定时间(StartTimeout)内没有做出回应的Guest Client将被踢出房间
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task StartGame()
    {
        if (!this.CanStartGame)
        {
            throw new InvalidOperationException();
        }

        await Task.Delay(1).ConfigureAwait(false);

        this.acceptGuestsCancelSource.Cancel();

        this.LobbyInfo.IsStarted = true;

        // 初始化服务器
        this.Session = new HostSession(serverProcessLauncher);
        await this.Session.Init();

        // 告诉 Guest 可以连接服务器

        this.Logger.Information("Waiting clients connect server...");
        foreach (var v in this.guests)
        {
            v.StartConnectServer();
        }
        this.LobbyInfo.CanConnectServer = true;

        while (true)
        {
            this.DisposeToken.ThrowIfCancellationRequested();
            await Task.Yield();
            var finish = true;
            foreach (var v in this.guests)
            {
                if (v.IsDisposed)
                {
                    continue;
                }
                if (!v.IsConnectedServer)
                {
                    finish = false;
                }
            }
            if (finish)
            {
                break;
            }
        }

        this.Logger.Information("Starting game...");
        this.Session.StartGame();

        _ = Task.Delay(-1, this.Session.DisposeToken)
            .ContinueWith(_ => this.Dispose(), TaskContinuationOptions.OnlyOnCanceled);
    }

    public Task Init()
    {
        this.Logger.Information("Initializing host client...");

        _ = this.AcceptNewConnectTask();
        return Task.CompletedTask;
    }

    private async Task AcceptNewConnectTask()
    {
        await Task.Yield().ConfigureAwait(false);
        while (!this.LobbyInfo.IsStarted)
        {
            await Task.Yield();
            this.DisposeToken.ThrowIfCancellationRequested();
            cancellationToken.ThrowIfCancellationRequested();

            this.Logger.Information("Waiting connection...");
            var connect = await listener.WaitConnect(cancellationToken.Combine(this.DisposeToken, this.acceptGuestsCancelSource.Token));

            this.Logger.Information("Connecting...");
            var gConnect = new GuestConnection(this, connect);
            this.guests.Add(gConnect);
            gConnect.Init();

            //第一个玩家为 Owner (通常为 房主)
            if (string.IsNullOrEmpty(this.LobbyInfo.Owner) ||
                !this.LobbyInfo.Guests.ContainsKey(this.LobbyInfo.Owner))
            {
                this.LobbyInfo.Owner = gConnect.GuestInfo.Guid;
                gConnect.GuestInfo.IsHost = true;
            }
        }
    }

    protected override void MyDispose()
    {
        base.MyDispose();
        this.acceptGuestsCancelSource.Cancel();
        listener.Dispose();
        this.Session?.Dispose();
        this.Session = null;

        foreach (var v in this.guests.ToArray())
        {
            v.Dispose();
        }

        this.guests.Clear();
    }

    void IOnGuestQuit.OnGuestQuit(GuestInfo guest)
    {
        if (guest.IsHost)
        {
            // 房主退出
            this.Dispose();
        }
    }
}
