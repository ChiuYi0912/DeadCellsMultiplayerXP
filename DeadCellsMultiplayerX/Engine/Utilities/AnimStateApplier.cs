namespace DeadCellsMultiplayerX.Engine.Utilities;

using dc.libs.heaps.slib;
using dc.libs.heaps.slib._AnimManager;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using ModCore.Utilities;

/// <summary>
/// Shared animation snapshot selection and playback.  Entity-specific state
/// (hero roll guard, mob tracker table) stays in the caller.
/// </summary>
internal static class AnimStateApplier
{
    public static AnimInfo? Select(
        AnimInfo? previous,
        AnimInfo? next,
        double renderTimeSec)
    {
        if (previous == null && next == null)
        {
            return null;
        }

        if (next == null)
        {
            return previous;
        }

        if (previous != null)
        {
            var nextStartSec = next.StartTime / 1000.0;
            if (renderTimeSec < nextStartSec)
            {
                return previous;
            }
        }

        return next;
    }

    /// <summary>
    /// Applies the chosen animation.  Returns true when a new clip was
    /// started, so the caller can update its last-group/last-start tracking.
    /// </summary>
    public static bool Play(
        AnimManager? anim,
        AnimInfo chosen,
        string lastGroup,
        long lastStartTime)
    {
        if (anim == null || chosen == null)
        {
            return false;
        }

        var groupName = chosen.GroupName;
        if (string.IsNullOrEmpty(groupName))
        {
            return false;
        }

        var needPlay = lastStartTime != chosen.StartTime
                     || lastGroup != groupName;

        if (needPlay)
        {
            if (anim.stack?.getDyn(0) is AnimInstance current)
            {
                current.plays = 0;
            }

            anim.play(groupName.AsHaxeString(), chosen.Plays, null);
        }

        var stack = anim.stack?.getDyn(0) as AnimInstance;
        if (stack != null)
        {
            stack.speed = chosen.Speed;
            stack.paused = chosen.Paused;
            stack.playDuration = chosen.PlayDuration;
        }

        return needPlay;
    }
}
