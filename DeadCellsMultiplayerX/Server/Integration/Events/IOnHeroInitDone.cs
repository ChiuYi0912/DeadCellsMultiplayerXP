using dc.en;
using ModCore.Events;
namespace DeadCellsMultiplayerX.Server.Integration.Events
{
    [Event]
    public interface IOnHeroInitDone
    {
        void OnHeroInitDone(Hero hero);
    }
}
