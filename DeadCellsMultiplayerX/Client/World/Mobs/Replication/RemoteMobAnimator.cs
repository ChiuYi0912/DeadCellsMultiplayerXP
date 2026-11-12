namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using System.Runtime.CompilerServices;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;

/// <summary>
/// 远端怪物动画应用器
/// </summary>
internal static class RemoteMobAnimator
{
    private sealed class Tracker
    {
        public string LastGroup { get; set; } = string.Empty;
        public long StartTime { get; set; } = -1;
    }

    private static readonly ConditionalWeakTable<Mob, Tracker> Trackers = [];

    public static void Apply(Mob mob, AnimInfo prevAnim, AnimInfo nextAnim, double renderTimeSec)
    {
        var anim = mob?.spr?.get_anim();
        if (mob == null || anim == null)
        {
            return;
        }

        var chosen = AnimStateApplier.Select(prevAnim, nextAnim, renderTimeSec);
        if (chosen == null)
        {
            return;
        }

        var tracker = Trackers.GetOrCreateValue(mob);
        if (AnimStateApplier.Play(
                anim, chosen, tracker.LastGroup, tracker.StartTime))
        {
            tracker.LastGroup = chosen.GroupName;
            tracker.StartTime = chosen.StartTime;
        }
    }
}
