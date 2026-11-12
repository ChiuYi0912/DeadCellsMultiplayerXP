using dc.pr;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Client.UI.Lobby;
using DeadCellsMultiplayerX.Client.Session;

namespace DeadCellsMultiplayerX.Client.UI
{
    internal class LobbyMenuX : UIProcess
    {
        private readonly LobbyUI lobby;

        public LobbyMenuX(ClientRuntime client, TitleScreen titleScreen) : base(titleScreen, dc.Const.Class.ROOT_DP_MENU)
        {
            lobby = new LobbyUI(client, titleScreen, this);
            lobby.OnCloseRequested += OnLobbyCloseRequested;
        }

        public override void update()
        {
            base.update();
        }

        private void OnLobbyCloseRequested()
        {
            Dispose();
        }

        protected override void MyDispose()
        {
            if (lobby != null)
            {
                lobby.OnCloseRequested -= OnLobbyCloseRequested;
                lobby.Dispose();
            }
            TitleScreen.Class.ME?.unblur();
            TitleScreen.Class.ME?.controller.manualLock = false;
        }
    }
}
