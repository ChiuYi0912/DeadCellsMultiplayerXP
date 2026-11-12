namespace DeadCellsMultiplayerX.Client.World.Frames;

/// <summary>
/// A generic, timestamp-based snapshot buffer for interpolating any state type
/// <typeparamref name="T"/>. Unlike <see cref="SnapshotInterpolationBuffer"/> (which is hard-wired
/// to the arena's <see cref="PlayerState"/> and a fixed 30 Hz tick), this buffer keys snapshots by
/// a <c>double</c> timestamp (seconds) and only computes the bracketing snapshots plus the
/// interpolation alpha. The type-specific Lerp is left to the caller, because it depends on how the
/// caller's state is laid out (e.g. Dead Cells double pixel coordinates).
/// </summary>
public sealed class SnapshotBuffer<T>(double interpolationDelaySeconds)
{
    private readonly List<(double time, T state)> buffer = [];
    private readonly double delaySeconds = interpolationDelaySeconds;

    public int Count => this.buffer.Count;

    /// <summary>
    /// 只读历史视图（旧 -> 新，与内部存储同序）。不要在枚举期间调用会修改缓冲区的 API。
    /// </summary>
    public IReadOnlyList<(double time, T state)> Entries => this.buffer;

    /// <summary>
    /// Ingests a snapshot for time <paramref name="time"/>. Out-of-order arrivals are inserted in
    /// sorted position; an exact duplicate timestamp is ignored.
    /// </summary>
    public void AddSnapshot(double time, T state)
    {
        if (this.buffer.Count > 0 && time <= this.buffer[^1].time)
        {
            var insertionIndex = this.buffer.FindIndex(entry => entry.time >= time);
            if (this.buffer[insertionIndex].time == time)
            {
                return; // duplicate timestamp, ignore
            }

            this.buffer.Insert(insertionIndex, (time, state));
            return;
        }

        this.buffer.Add((time, state));
    }

    public void Clear() => this.buffer.Clear();

    /// <summary>
    /// 丢弃时间早于 <paramref name="oldestTimeInclusive"/> 的样本，并把数量限制在
    /// <paramref name="maxCount"/> 以内（超限时优先删除最旧的样本）。
    /// </summary>
    public void Trim(double oldestTimeInclusive, int maxCount)
    {
        var removeCount = 0;
        while (removeCount < this.buffer.Count && this.buffer[removeCount].time < oldestTimeInclusive)
        {
            removeCount++;
        }

        var overflow = this.buffer.Count - removeCount - maxCount;
        if (overflow > 0)
        {
            removeCount += overflow;
        }

        if (removeCount > 0)
        {
            this.buffer.RemoveRange(0, removeCount);
        }
    }

    /// <summary>
    /// Finds the two snapshots that bracket the render time for <paramref name="localTimeSeconds"/>
    /// (render time = localTime - interpolation delay) and returns them plus the interpolation
    /// alpha between them. Returns <c>false</c> only when the buffer is empty. When the render time
    /// falls before the first snapshot or past the last one, <paramref name="previous"/> and
    /// <paramref name="next"/> are both the nearest snapshot and alpha is 0 (hold, do not
    /// extrapolate). Also prunes snapshots that are now fully in the past.
    /// </summary>
    public bool TryGetInterpolation(double localTimeSeconds, out T previous, out T next, out double alpha)
    {
        if (this.buffer.Count == 0)
        {
            previous = default!;
            next = default!;
            alpha = 0;
            return false;
        }

        var renderTime = localTimeSeconds - this.delaySeconds;

        if (renderTime <= this.buffer[0].time)
        {
            previous = next = this.buffer[0].state;
            alpha = 0;
            return true;
        }

        for (var i = 0; i < this.buffer.Count - 1; i++)
        {
            var (timeA, stateA) = this.buffer[i];
            var (timeB, stateB) = this.buffer[i + 1];

            if (renderTime >= timeA && renderTime <= timeB)
            {
                this.PruneBefore(i);
                previous = stateA;
                next = stateB;
                alpha = timeB == timeA ? 0 : (renderTime - timeA) / (timeB - timeA);
                return true;
            }
        }

        // Render time is ahead of every snapshot (buffer underrun): hold the last known state.
        var (time, state) = this.buffer[^1];
        this.PruneBefore(this.buffer.Count - 1);
        previous = next = state;
        alpha = 0;
        return true;
    }

    private void PruneBefore(int keepFromIndex)
    {
        if (keepFromIndex > 0)
        {
            this.buffer.RemoveRange(0, keepFromIndex);
        }
    }
}
