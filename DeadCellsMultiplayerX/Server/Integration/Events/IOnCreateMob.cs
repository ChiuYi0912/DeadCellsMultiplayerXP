namespace DeadCellsMultiplayerX.Server.Integration.Events;

using ModCore.Events;

[Event]
public interface IOnCreateMob
{
    public record Data(dc.en.Mob mob, dc.String k, int cx, int cy, int dmgTier, int? lifeTier);
    public void OnCreateMob(Data data);
}
