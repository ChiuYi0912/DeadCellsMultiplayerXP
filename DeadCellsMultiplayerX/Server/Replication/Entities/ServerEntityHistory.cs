namespace DeadCellsMultiplayerX.Server.Replication.Entities;

using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>
/// 历史实体位置
/// </summary>
internal readonly record struct EntityPose(
    double X,
    double Y,
    int Dir,
    double Dx,
    double Dy);

/// <summary>
/// 服务端实体位置历史
/// 每个 20Hz 发布帧记录一次，用于武器命中回退
/// </summary>
internal sealed class ServerEntityHistory
{
    private const int MAX_FRAMES = 128;

    private readonly List<HistoryFrame> frames = [];

    public bool TryGetPose(uint netId, long timeMs, out EntityPose pose)
    {
        pose = default;

        if (this.frames.Count == 0)
        {
            return false;
        }

        HistoryFrame? previous = null;
        HistoryFrame? next = null;

        for (var i = 0; i < this.frames.Count; i++)
        {
            var frame = this.frames[i];

            if (frame.TimeMs <= timeMs)
            {
                previous = frame;
                continue;
            }

            next = frame;
            break;
        }

        previous ??= this.frames[0];
        next ??= this.frames[^1];

        var hasPrevious = previous.Poses.TryGetValue(netId, out var previousPose);
        var hasNext = next.Poses.TryGetValue(netId, out var nextPose);

        if (!hasPrevious && !hasNext)
        {
            return false;
        }

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

        var alpha = (timeMs - previous.TimeMs)
            / (double)(next.TimeMs - previous.TimeMs);

        alpha = Math.Clamp(alpha, 0.0, 1.0);

        pose = new EntityPose(
            previousPose.X + ((nextPose.X - previousPose.X) * alpha),
            previousPose.Y + ((nextPose.Y - previousPose.Y) * alpha),
            alpha < 0.5 ? previousPose.Dir : nextPose.Dir,
            alpha < 0.5 ? previousPose.Dx : nextPose.Dx,
            alpha < 0.5 ? previousPose.Dy : nextPose.Dy);

        return true;
    }

    public void Record(long serverTimeMs, IReadOnlyList<EntityDynamic> entities)
    {
        var frame = new HistoryFrame { TimeMs = serverTimeMs };

        if (entities != null)
        {
            foreach (var entity in entities)
            {
                if (entity?.Pos == null)
                {
                    continue;
                }

                frame.Poses[entity.NetId] = new EntityPose(
                    entity.Pos.CX + entity.Pos.XR,
                    entity.Pos.CY + entity.Pos.XY,
                    entity.Pos.DIR,
                    entity.Pos.DX,
                    entity.Pos.DY);
            }
        }

        this.frames.Add(frame);

        if (this.frames.Count > MAX_FRAMES)
        {
            this.frames.RemoveRange(0, this.frames.Count - MAX_FRAMES);
        }
    }

    private sealed class HistoryFrame
    {
        public long TimeMs { get; set; }
        public Dictionary<uint, EntityPose> Poses { get; } = [];
    }
}
