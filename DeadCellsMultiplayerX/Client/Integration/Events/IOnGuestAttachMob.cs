using ModCore.Events;
namespace DeadCellsMultiplayerX.Client.Integration.Events
{
    [Event]
    public interface IOnGuestAttachMob
    {
        public record class Data(dc.level.Mob mobdata, dc.en.Mob mob);
        public void OnAttachMob(Data data);
    }
}
