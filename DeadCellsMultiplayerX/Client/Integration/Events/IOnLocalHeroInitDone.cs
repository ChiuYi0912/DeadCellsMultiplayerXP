namespace DeadCellsMultiplayerX.Client.Integration.Events;

using dc.en;
using DeadCellsMultiplayerX.Client.Session;
using ModCore.Events;

/// <summary>
/// 客户端本地hero初始化事件
/// </summary>
/// <remarks>
/// 挂载至:<see cref="ClientRuntime.Hook_Game_init(dc.pr.Hook_Game.orig_init, dc.pr.Game)"/>
/// </remarks>
[Event]
public interface IOnLocalHeroInitDone
{
    public void OnHeroInitDone(Hero hero);
}
