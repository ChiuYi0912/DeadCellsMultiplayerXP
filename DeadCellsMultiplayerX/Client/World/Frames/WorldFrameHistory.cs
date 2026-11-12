namespace DeadCellsMultiplayerX.Client.World.Frames;

using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Time;

/// <summary>
/// 已应用世界帧的渲染历史：负责插值查询与前后帧定位。
/// 不处理序列、NACK、epoch 或重同步。
/// </summary>
internal sealed class WorldFrameHistory
{
    private const double INTERPOLATION_DELAY_SECONDS =
        SnapshotTiming.INTERPOLATION_DELAY_SECONDS;

    /// <summary>
    /// 跨越大段空白时不再插值。
    /// </summary>
    private const double MAX_CONTIGUOUS_GAP_SECONDS = 0.5;

    /// <summary>
    /// 历史保留时长。
    /// </summary>
    private const double KEEP_SECONDS = 2.0;

    /// <summary>
    /// 历史最大帧数。
    /// </summary>
    private const int MAX_FRAMES = 64;

    private readonly SnapshotBuffer<WorldFrame> timeline =
        new(INTERPOLATION_DELAY_SECONDS);

    public int Count => this.timeline.Count;

    /// <summary>
    /// 写入一帧并按服务端时间裁剪历史。
    /// </summary>
    public void Add(WorldFrame frame, double timeSec)
    {
        var entries = this.timeline.Entries;
        if (entries.Count > 0
            && timeSec - entries[^1].time > MAX_CONTIGUOUS_GAP_SECONDS)
        {
            this.timeline.Clear();
        }

        this.timeline.AddSnapshot(timeSec, frame);
        this.timeline.Trim(timeSec - KEEP_SECONDS, MAX_FRAMES);
    }

    public void Clear() => this.timeline.Clear();

    public bool TryGetHeroBracket(
        string guid,
        double nowSec,
        out HeroDynamic previous,
        out HeroDynamic next,
        out double alpha,
        out double renderTimeSec)
    {
        previous = null!;
        next = null!;
        alpha = 0;
        // 渲染时间回退一个插值延迟
        renderTimeSec = nowSec - INTERPOLATION_DELAY_SECONDS;

        if (!this.timeline.TryGetInterpolation(
                nowSec, out var prevFrame, out var nextFrame, out alpha))
        {
            return false;
        }

        var prev = FindHero(prevFrame, guid);
        var nextState = FindHero(nextFrame, guid);

        if (prev == null && nextState == null)
        {
            return false;
        }

        // 关卡变化是瞬移，取较新的那一侧
        if (prev != null && nextState != null
            && (prev.LevelId != nextState.LevelId
                || prev.SubLevelIndex != nextState.SubLevelIndex))
        {
            previous = nextState;
            next = nextState;
            alpha = 0;
            return true;
        }

        previous = prev ?? nextState!;
        next = nextState ?? prev!;
        return true;
    }

    public bool TryGetEntityBracket(
        uint netId,
        double nowSec,
        out EntityDynamic previous,
        out EntityDynamic next,
        out double alpha,
        out double renderTimeSec)
    {
        previous = null!;
        next = null!;
        alpha = 0;
        renderTimeSec = nowSec - INTERPOLATION_DELAY_SECONDS;

        if (!this.timeline.TryGetInterpolation(
                nowSec, out var prevFrame, out var nextFrame, out alpha))
        {
            return false;
        }

        var prev = FindEntity(prevFrame, netId);
        var nextState = FindEntity(nextFrame, netId);

        if (prev == null && nextState == null)
        {
            return false;
        }

        // 关卡变化是瞬移，取较新的那一侧
        if (prev != null && nextState != null
            && (prev.LevelId != nextState.LevelId
                || prev.SubLevelIndex != nextState.SubLevelIndex))
        {
            previous = nextState;
            next = nextState;
            alpha = 0;
            return true;
        }

        previous = prev ?? nextState!;
        next = nextState ?? prev!;
        return true;
    }

    private static HeroDynamic? FindHero(WorldFrame frame, string guid)
    {
        if (frame?.Heroes == null)
        {
            return null;
        }

        foreach (var hero in frame.Heroes)
        {
            if (hero != null && hero.GUID == guid)
            {
                return hero;
            }
        }

        return null;
    }

    private static EntityDynamic? FindEntity(WorldFrame frame, uint netId)
    {
        if (frame?.Entities == null)
        {
            return null;
        }

        foreach (var entity in frame.Entities)
        {
            if (entity != null && entity.NetId == netId)
            {
                return entity;
            }
        }

        return null;
    }
}
