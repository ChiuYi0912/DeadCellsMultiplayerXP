

using ModCore.Events;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    public interface IOnLobbyMenuDisposed
    {
        void OnLobbyMenuDisposed();
    }
}