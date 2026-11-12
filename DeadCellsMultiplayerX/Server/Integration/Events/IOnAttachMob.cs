using ModCore.Events;
namespace DeadCellsMultiplayerX.Server.Integration.Events
{
    [Event]
    public interface IOnAttachMob
    {
        public record class Data(dc.level.Mob mobdata, dc.en.Mob mob);
        public void OnAttachMob(Data data);
    }
}
