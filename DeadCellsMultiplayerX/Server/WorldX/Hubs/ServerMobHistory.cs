using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Server.WorldX.Hubs
{
    /// <summary>
    /// 历史 mob 位置
    /// </summary>
    internal readonly record struct MobPose(
        double X,
        double Y,
        int Dir,
        double Dx,
        double Dy
    );

    /// <summary>
    /// 服务端 mob 位置历史
    /// 每个 20Hz 发布帧记录一次，用于武器命中回退
    /// </summary>
    internal sealed class ServerMobHistory
    {
        private const int MaxFrames = 128;

        private readonly List<HistoryFrame> frames = [];

        /// <summary>
        /// 查询指定时间点的 mob 位置
        /// </summary>
        public bool TryGetPose(uint netId, long timeMs, out MobPose pose)
        {
            pose = default;

            if (frames.Count == 0)
                return false;

            HistoryFrame? previous = null;
            HistoryFrame? next = null;

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];

                if (frame.TimeMs <= timeMs)
                {
                    previous = frame;
                    continue;
                }

                next = frame;
                break;
            }

            previous ??= frames[0];
            next ??= frames[^1];

            bool hasPrevious = previous.Poses.TryGetValue(netId, out var previousPose);
            bool hasNext = next.Poses.TryGetValue(netId, out var nextPose);

            if (!hasPrevious && !hasNext)
                return false;

            if (!hasPrevious)
            {
                pose = nextPose;
                return true;
            }

            if (!hasNext)
            {
                pose = previousPose;
                return true;
            }

            if (next.TimeMs <= previous.TimeMs)
            {
                pose = previousPose;
                return true;
            }

            double alpha = (timeMs - previous.TimeMs)
                / (double)(next.TimeMs - previous.TimeMs);

            alpha = Math.Clamp(alpha, 0.0, 1.0);

            pose = new MobPose(
                previousPose.X + (nextPose.X - previousPose.X) * alpha,
                previousPose.Y + (nextPose.Y - previousPose.Y) * alpha,
                alpha < 0.5 ? previousPose.Dir : nextPose.Dir,
                alpha < 0.5 ? previousPose.Dx : nextPose.Dx,
                alpha < 0.5 ? previousPose.Dy : nextPose.Dy);

            return true;
        }

        /// <summary>
        /// 记录一个发布帧的 mob 位置
        /// </summary>
        public void Record(long serverTimeMs, IReadOnlyList<MobDynamic> mobs)
        {
            var frame = new HistoryFrame { TimeMs = serverTimeMs };

            if (mobs != null)
            {
                foreach (var mob in mobs)
                {
                    if (mob?.Pos == null) continue;

                    frame.Poses[mob.NetId] = new MobPose(
                        mob.Pos.CX + mob.Pos.XR,
                        mob.Pos.CY + mob.Pos.XY,
                        mob.Pos.DIR,
                        mob.Pos.DX,
                        mob.Pos.DY);
                }
            }

            frames.Add(frame);

            if (frames.Count > MaxFrames)
                frames.RemoveRange(0, frames.Count - MaxFrames);
        }

        private sealed class HistoryFrame
        {
            public long TimeMs;
            public readonly Dictionary<uint, MobPose> Poses = [];
        }
    }


}
