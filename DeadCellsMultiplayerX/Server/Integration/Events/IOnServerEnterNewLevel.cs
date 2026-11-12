namespace DeadCellsMultiplayerX.Server.Integration.Events;

using ModCore.Events;

[Event]
internal interface IOnServerEnterNewLevel
{
    public void OnServerEnterNewLevel();
}
