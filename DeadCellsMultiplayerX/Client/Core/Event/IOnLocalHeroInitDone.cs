using dc.en;
using ModCore.Events;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Core.Event
{

    /// <summary>
    /// 客户端本地hero初始化事件
    /// </summary>
    /// <remarks>
    /// 挂载至:<see cref="ClientMain.Hook_Game_init(dc.pr.Hook_Game.orig_init, dc.pr.Game)"/>
    /// </remarks>
    [Event]
    public interface IOnLocalHeroInitDone
    {
        void OnHeroInitDone(Hero hero);
    }
}