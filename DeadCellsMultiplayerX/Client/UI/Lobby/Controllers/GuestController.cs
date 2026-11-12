namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers;

using DeadCellsMultiplayerX.Client.Session;

internal sealed class GuestController(
    ClientRuntime client,
    LobbyContext ctx,
    LobbyStateMachine state) : LobbyController(client, ctx, state)
{

    //加入房间
    public Task JoinRoom(string ip, int port)
        => this.RunAsync(async ct =>
        {
            if (!this.State.TryGo(LobbyState.Connecting))
            {
                return;
            }

            this.Ctx.HostIp = ip;
            this.Ctx.HostPort = port;
            this.Ctx.IsHost = false;

            await this.Client.StartGuest(ip, port).WaitAsync(ct);

            var guest = this.Client.CurrentGuestClient
                ?? throw new InvalidOperationException("Guest client not created");

            this.Ctx.LocalGuid = guest.Guid;
            this.Ctx.Lobby = guest.LobbyInfo;

            if (!this.State.TryGo(LobbyState.InRoom))
            {
                return;
            }
        }, "JoinRoom");

    //客户端准备
    public void ToggleReady()
    {
        if (this.IsDisposed)
        {
            return;
        }

        if (this.State.Current != LobbyState.InRoom)
        {
            return;
        }

        var gc = this.Client.CurrentGuestClient;
        if (gc == null)
        {
            return;
        }

        var me = this.Ctx.Me;
        if (me == null)
        {
            return;
        }

        var newReady = !me.IsReady;
        gc.SetReady(newReady);
        me.IsReady = newReady;
    }

    //离开
    public void LeaveRoom()
    {
        this.Client.CurrentGuestClient?.Quit();
        this.Ctx.Reset();

        if (this.State.CanGo(LobbyState.MainMenu))
        {
            this.State.TryGo(LobbyState.MainMenu);
        }
    }

    protected override void MyDispose() { }
}
