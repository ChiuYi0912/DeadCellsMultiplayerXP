using ModCore.Events;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    public interface IOnCreateMob
    {
        public record Data(dc.en.Mob mob, dc.String k, int cx, int cy, int dmgTier, int? lifeTier);
        void OnCretaMob(Data data);
    }
}