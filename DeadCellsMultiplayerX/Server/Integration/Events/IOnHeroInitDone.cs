namespace DeadCellsMultiplayerX.Server.Integration.Events;

using dc.en;
using ModCore.Events;

[Event]
public interface IOnHeroInitDone
{
    public void OnHeroInitDone(Hero hero);
}
