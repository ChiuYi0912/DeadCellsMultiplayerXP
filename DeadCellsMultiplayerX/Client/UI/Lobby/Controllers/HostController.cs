namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers;

using DeadCellsMultiplayerX.Client.Session;

internal sealed class HostController(
    ClientRuntime client,
    LobbyContext ctx,
    LobbyStateMachine state) : LobbyController(client, ctx, state)
{

    //创建房间
    public Task CreateRoom(string ip, int port)
        => this.RunAsync(async ct =>
        {
            if (!this.State.TryGo(LobbyState.Connecting))
            {
                return;
            }

            this.Ctx.HostIp = ip;
            this.Ctx.HostPort = port;
            this.Ctx.IsHost = true;

            await this.Client.StartHost(ip, port).WaitAsync(ct);

            var guest = this.Client.CurrentGuestClient
                ?? throw new InvalidOperationException("Host guest client not created");

            guest.SetReady(true);
            this.Ctx.LocalGuid = guest.Guid;
            this.Ctx.Lobby = guest.LobbyInfo;

            if (!this.State.TryGo(LobbyState.InRoom))
            {
                return;
            }
        }, "CreateRoom");

    //房主开始游戏
    public Task StartGame()
        => this.RunAsync(async ct =>
        {
            if (!this.CanStartGame(out var reason))
            {
                this.Fail(reason);
                return;
            }

            if (!this.State.TryGo(LobbyState.StartingGame))
            {
                return;
            }

            var hc = this.Client.CurrentHostClient
                ?? throw new InvalidOperationException("Host client not found");

            await hc.StartGame().WaitAsync(ct);
        }, "StartGame");

    private bool CanStartGame(out string reason)
    {
        reason = string.Empty;

        var players = this.Client.CurrentGuestClient?.LobbyInfo?.Guests;
        if (players == null)
        {
            reason = "连接异常";
            return false;
        }

        var unready = players.Values
            .Where(p => !p.IsReady)
            .Select(p => p.Name)
            .ToArray();

        if (unready.Length > 0)
        {
            reason = $"玩家 {string.Join(",", unready)} 未准备";
            return false;
        }

        return true;
    }

    public void DestroyRoom()
    {
        this.Client.CurrentHostClient?.Dispose();
        this.Client.CurrentGuestClient?.Quit();

        this.Ctx.Reset();

        if (this.State.CanGo(LobbyState.MainMenu))
        {
            this.State.TryGo(LobbyState.MainMenu);
        }
    }

    protected override void MyDispose() { }
}
