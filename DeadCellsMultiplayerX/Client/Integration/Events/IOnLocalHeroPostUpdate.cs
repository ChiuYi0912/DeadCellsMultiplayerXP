namespace DeadCellsMultiplayerX.Client.Integration.Events;


using dc.en;
using DeadCellsMultiplayerX.Client.Session;
using ModCore.Events;

/// <summary>
/// 用于收集本地客户端快照信息
/// </summary>
/// <remarks>每帧更新,挂载至:<see cref="ClientRuntime.Hook_Hero_postUpdate(Hook_Hero.orig_postUpdate, Hero)"/></remarks>
[Event]
public interface IOnLocalHeroPostUpdate
{
    public void OnPostUpdate(Hero hero);
}
