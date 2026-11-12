using ModCore.Events;
namespace DeadCellsMultiplayerX.Server.Integration.Events
{
    [Event]
    internal interface IOnServerEnterNewLevel
    {
        public void OnServerEnterNewLevel();
    }
}
