using System.Runtime.CompilerServices;
using CoreLibrary.Core.Extensions;
using dc.en;
using dc.libs.heaps.slib._AnimManager;
using DeadCellsMultiplayerX.Common.Data;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Client.Core;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;

namespace DeadCellsMultiplayerX.Client.Mobs
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
            if (prevAnim == null && nextAnim == null) return;

            AnimInfo chosen = nextAnim;
            if (nextAnim == null)
            {
                chosen = prevAnim!;
            }
            else if (prevAnim != null)
            {
                double nextStartSec = nextAnim.StartTime / 1000.0;
                if (renderTimeSec < nextStartSec)
                    chosen = prevAnim;
            }

            if (chosen == null) return;

            string groupName = chosen.GroupName;
            if (string.IsNullOrEmpty(groupName)) return;

            var tracker = trackers.GetOrCreateValue(mob);
            bool needPlay = tracker.StartTime != chosen.StartTime
                         || tracker.LastGroup != groupName;

            if (needPlay)
            {
                tracker.LastGroup = groupName;
                tracker.StartTime = chosen.StartTime;

                var cur = anim.stack?.getDyn(0) as AnimInstance;
                if (cur != null) cur.plays = 0;

                anim.play(groupName.AsHaxeString(), chosen.Plays, null);
            }

            var stack = anim.stack?.getDyn(0) as AnimInstance;
            if (stack != null)
            {
                stack.speed = chosen.Speed;
                stack.paused = chosen.Paused;
                stack.playDuration = chosen.playDuration;
            }
        }
    }
}
