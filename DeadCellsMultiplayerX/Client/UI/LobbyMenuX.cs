namespace DeadCellsMultiplayerX.Client.UI;

using dc.pr;
using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby;

internal sealed class LobbyMenuX : UIProcess
{
    private readonly LobbyUI lobby;

    public LobbyMenuX(ClientRuntime client, TitleScreen titleScreen) : base(titleScreen, dc.Const.Class.ROOT_DP_MENU)
    {
        this.lobby = new LobbyUI(client, titleScreen, this);
        this.lobby.OnCloseRequested += this.OnLobbyCloseRequested;
    }

    public override void update() => base.update();

    private void OnLobbyCloseRequested() => this.Dispose();

    protected override void MyDispose()
    {
        if (this.lobby != null)
        {
            this.lobby.OnCloseRequested -= this.OnLobbyCloseRequested;
            this.lobby.Dispose();
        }
        TitleScreen.Class.ME?.unblur();
        TitleScreen.Class.ME?.controller.manualLock = false;
    }
}
