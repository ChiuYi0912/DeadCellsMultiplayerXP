namespace DeadCellsMultiplayerX.Networking.Frames;

internal enum FrameOverflowMode
{
    /// <summary>Drop pending frames and request a full baseline.</summary>
    RequestResync,

    /// <summary>Drop pending frames and jump to the newest received frame.</summary>
    JumpToNewest,
}

internal enum FrameGapSkipMode
{
    /// <summary>Skip to the earliest pending frame and drain it.</summary>
    Earliest,

    /// <summary>Skip to the newest pending frame and accept only it.</summary>
    Newest,
}

/// <summary>
/// Per-stream reliability policy.  The defaults describe the client receive
/// stream (64 pending, resync on large gaps, skip to earliest).  The server
/// upload collector overrides them to preserve its existing behavior.
/// </summary>
internal sealed class FrameSequencerPolicy
{
    public int MaxPending { get; init; } = 64;
    public int MaxNackRange { get; init; } = 64;
    public long NackFirstDelayMs { get; init; } = 50;
    public long NackRetryMs { get; init; } = 100;
    public long GapSkipMs { get; init; } = 250;
    public int GapSkipCount { get; init; } = 16;

    public bool ResyncOnLargeGap { get; init; }
    public long ResyncGapMs { get; init; } = 1000;
    public int ResyncGapFrameCount { get; init; } = 64;

    public FrameOverflowMode OverflowMode { get; init; } =
        FrameOverflowMode.RequestResync;
    public FrameGapSkipMode GapSkipMode { get; init; } =
        FrameGapSkipMode.Earliest;

    /// <summary>On an unavailable NACK response, jump to the newest frame.</summary>
    public bool SkipOnUnavailable { get; init; }
}

/// <summary>
/// Result of one NACK request.  <see cref="Resync"/> carries a role-specific
/// baseline object (the sequencer never inspects it).
/// </summary>
internal readonly record struct FrameSequencerBatch<TFrame>(
    IReadOnlyList<TFrame> Frames,
    bool Unavailable = false,
    object? Resync = null)
    where TFrame : class
{
    public static FrameSequencerBatch<TFrame> Empty { get; } =
        new([]);
}

/// <summary>
/// Pure sequence / gap / NACK state machine shared by the client world-frame
/// receive path and the server client-frame upload path.
///
/// The sequencer is single-threaded: the owner calls Receive/Tick on the game
/// synchronization context.  It performs no I/O; NACK and resync are
/// callbacks owned by the endpoint.
/// </summary>
internal sealed class FrameSequencer<TFrame>(
    FrameSequencerPolicy policy,
    Func<TFrame, long> sequenceOf,
    Action<TFrame> applyFrame,
    bool hasBaseline = false,
    Func<long, long, Task<FrameSequencerBatch<TFrame>>>? requestFrames = null,
    Action<string>? requestResync = null,
    Action<object>? applyResync = null,
    Action<string>? log = null) where TFrame : class
{
    private readonly FrameSequencerPolicy policy = policy ?? throw new ArgumentNullException(nameof(policy));
    private readonly Func<TFrame, long> sequenceOf = sequenceOf ?? throw new ArgumentNullException(nameof(sequenceOf));
    private readonly Action<TFrame> applyFrame = applyFrame ?? throw new ArgumentNullException(nameof(applyFrame));
    private readonly Func<long, long, Task<FrameSequencerBatch<TFrame>>>? requestFrames = requestFrames;
    private readonly Action<string>? requestResync = requestResync;
    private readonly Action<object>? applyResync = applyResync;
    private readonly Action<string>? log = log;

    private readonly SortedDictionary<long, TFrame> pending = [];
    private long? gapSinceMs;
    private long lastNackMs;
    private Task<FrameSequencerBatch<TFrame>>? nackTask;

    public bool HasBaseline { get; private set; } = hasBaseline;
    public long LastDelivered { get; private set; } = -1;
    public long NextExpected { get; private set; }
    public int PendingCount => this.pending.Count;

    public int GapCount { get; private set; }
    public int NackSent { get; private set; }
    public int BundleReceived { get; private set; }
    public int SkippedFrames { get; private set; }
    public int DroppedOld { get; private set; }
    public int PendingOverflow { get; private set; }

    /// <summary>Requests the initial baseline when the stream starts without one.</summary>
    public void Start()
    {
        if (!this.HasBaseline)
        {
            this.requestResync?.Invoke("initial");
        }
    }

    /// <summary>Receives one frame; contiguous frames are applied immediately.</summary>
    public void Receive(TFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        var sequence = this.sequenceOf(frame);

        if (!this.HasBaseline)
        {
            this.BufferPending(sequence, frame);
            return;
        }

        if (sequence <= this.LastDelivered || this.pending.ContainsKey(sequence))
        {
            this.DroppedOld++;
            return;
        }

        if (sequence == this.NextExpected)
        {
            this.Accept(sequence, frame);
            this.DrainContiguous();
            return;
        }

        if (this.pending.Count >= this.policy.MaxPending)
        {
            this.PendingOverflow++;
            this.OnOverflow(sequence, frame);
            return;
        }

        this.pending[sequence] = frame;
        this.GapCount++;
    }

    /// <summary>Applies a baseline supplied outside the normal sequence.</summary>
    public void ApplyDirect(TFrame frame)
    {
        if (frame == null)
        {
            return;
        }

        this.applyFrame(frame);
    }

    /// <summary>Adopts a full baseline sequence; clears pending/gap state.</summary>
    public void AdoptBaseline(long sequence)
    {
        this.pending.Clear();
        this.LastDelivered = sequence;
        this.NextExpected = sequence + 1;
        this.gapSinceMs = null;
        this.lastNackMs = 0;
        this.nackTask = null;
        this.HasBaseline = true;
    }

    /// <summary>Advances gap/NACK scheduling.  Called once per game tick.</summary>
    public void Tick(long nowMs)
    {
        this.PollNack();

        if (!this.HasBaseline)
        {
            // The endpoint throttles repeated resync requests.
            this.requestResync?.Invoke("baseline retry");
            return;
        }

        if (this.pending.Count == 0)
        {
            this.gapSinceMs = null;
            return;
        }

        var missingFrom = this.NextExpected;
        var missingTo = this.pending.Keys.Min() - 1;

        if (missingTo < missingFrom)
        {
            this.DrainContiguous();
            return;
        }

        this.gapSinceMs ??= nowMs;
        var gapMs = nowMs - this.gapSinceMs.Value;
        var gapCount = missingTo - missingFrom + 1;

        if (this.policy.ResyncOnLargeGap
            && (gapCount > this.policy.ResyncGapFrameCount
                || gapMs > this.policy.ResyncGapMs))
        {
            this.log?.Invoke(
                $"Frame gap too large: from={missingFrom} to={missingTo}");
            this.requestResync?.Invoke("gap too large");
            return;
        }

        if (gapMs > this.policy.GapSkipMs || gapCount > this.policy.GapSkipCount)
        {
            this.log?.Invoke(
                $"Skip frame gap: from={missingFrom} to={missingTo}");
            this.SkipGap();
            return;
        }

        if (gapMs < this.policy.NackFirstDelayMs)
        {
            return;
        }

        if (this.nackTask != null && !this.nackTask.IsCompleted)
        {
            return;
        }

        if (nowMs - this.lastNackMs < this.policy.NackRetryMs)
        {
            return;
        }

        if (this.requestFrames == null)
        {
            return;
        }

        this.lastNackMs = nowMs;
        this.NackSent++;

        var to = Math.Min(
            missingTo, missingFrom + this.policy.MaxNackRange - 1);
        try
        {
            this.nackTask = this.requestFrames(missingFrom, to);
        }
        catch (Exception ex)
        {
            this.log?.Invoke(
                $"Frame NACK request failed: {ex.GetBaseException().Message}");
            this.nackTask = null;
        }
    }

    /// <summary>
    /// Records a frame dropped by an epoch/owner check before it reached the
    /// sequencer.
    /// </summary>
    public void NoteDroppedOld() => this.DroppedOld++;

    /// <summary>Clears sequencing state but keeps diagnostic counters.</summary>
    public void Reset()
    {
        this.pending.Clear();
        this.NextExpected = 0;
        this.LastDelivered = -1;
        this.HasBaseline = false;
        this.gapSinceMs = null;
        this.lastNackMs = 0;
        this.nackTask = null;
    }

    private void BufferPending(long sequence, TFrame frame)
    {
        if (this.pending.Count >= this.policy.MaxPending)
        {
            this.PendingOverflow++;
            this.OnOverflow(sequence, frame);
            return;
        }

        this.pending[sequence] = frame;
    }

    private void OnOverflow(long sequence, TFrame frame)
    {
        if (this.policy.OverflowMode == FrameOverflowMode.JumpToNewest)
        {
            this.pending.Clear();
            this.NextExpected = sequence;
            this.Accept(sequence, frame);
            return;
        }

        this.pending.Clear();
        this.gapSinceMs = null;
        this.requestResync?.Invoke("pending overflow");
    }

    private void Accept(long sequence, TFrame frame)
    {
        this.LastDelivered = sequence;
        this.NextExpected = sequence + 1;
        this.gapSinceMs = null;
        this.applyFrame(frame);
    }

    private void DrainContiguous()
    {
        while (this.pending.Remove(this.NextExpected, out var frame))
        {
            this.Accept(this.NextExpected, frame);
        }
    }

    private void SkipGap()
    {
        if (this.pending.Count == 0)
        {
            this.gapSinceMs = null;
            return;
        }

        if (this.policy.GapSkipMode == FrameGapSkipMode.Newest)
        {
            this.SkipToNewest();
            return;
        }

        var earliest = this.pending.Keys.Min();
        var skipped = earliest - this.NextExpected;
        if (skipped > 0)
        {
            this.SkippedFrames += (int)skipped;
        }

        this.NextExpected = earliest;
        this.DrainContiguous();
        this.gapSinceMs = null;
    }

    private void SkipToNewest()
    {
        if (this.pending.Count == 0)
        {
            this.gapSinceMs = null;
            return;
        }

        var newest = this.pending.Keys.Max();
        var skipped = newest - this.NextExpected;
        if (skipped > 0)
        {
            this.SkippedFrames += (int)skipped;
        }

        this.pending.Remove(newest, out var frame);
        this.pending.Clear();
        this.NextExpected = newest;

        if (frame != null)
        {
            this.Accept(newest, frame);
        }

        this.gapSinceMs = null;
    }

    private void PollNack()
    {
        if (this.nackTask == null || !this.nackTask.IsCompleted)
        {
            return;
        }

        var task = this.nackTask;
        this.nackTask = null;

        if (task.IsFaulted)
        {
            this.log?.Invoke($"Frame NACK failed: {task.Exception?.GetBaseException().Message}");
            return;
        }

        if (task.IsCanceled)
        {
            return;
        }

        this.BundleReceived++;
        var batch = task.Result;

        if (batch.Resync != null)
        {
            this.applyResync?.Invoke(batch.Resync);
            return;
        }

        if (batch.Unavailable && this.policy.SkipOnUnavailable)
        {
            this.SkipToNewest();
            return;
        }

        if (batch.Frames == null)
        {
            return;
        }

        foreach (var frame in batch.Frames)
        {
            this.Receive(frame);
        }
    }
}
