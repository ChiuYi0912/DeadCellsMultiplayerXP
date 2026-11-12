using System.Runtime.CompilerServices;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication
{
    /// <summary>
    /// 远端怪物动画应用器
    /// </summary>
    internal static class RemoteMobAnimator
    {
        private sealed class Tracker
        {
            public string LastGroup = string.Empty;
            public long StartTime = -1;
        }

        private static readonly ConditionalWeakTable<Mob, Tracker> trackers = new();

        public static void Apply(Mob mob, AnimInfo prevAnim, AnimInfo nextAnim, double renderTimeSec)
        {
            var anim = mob?.spr?.get_anim();
            if (mob == null || anim == null) return;

            var chosen = AnimStateApplier.Select(prevAnim, nextAnim, renderTimeSec);
            if (chosen == null) return;

            var tracker = trackers.GetOrCreateValue(mob);
            if (AnimStateApplier.Play(
                    anim, chosen, tracker.LastGroup, tracker.StartTime))
            {
                tracker.LastGroup = chosen.GroupName;
                tracker.StartTime = chosen.StartTime;
            }
        }
    }
}
