using ModCore.Events;
using DeadCellsMultiplayerX.Client.Session.Host;
using DeadCellsMultiplayerX.Contracts.Lobby;
namespace DeadCellsMultiplayerX.Client.Integration.Events
{
    /// <summary>
    /// 播报房主退出房间事件
    /// </summary>
    /// <remarks> 
    /// 挂载至:<see cref="GuestConnection.MyDispose"/>
    /// </remarks>
    [Event]
    internal interface IOnGuestQuit
    {
        public void OnGuestQuit(GuestInfo guest);
    }
}
