namespace DeadCellsMultiplayerX.Networking.Frames
{
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
            new(Array.Empty<TFrame>());
    }

    /// <summary>
    /// Pure sequence / gap / NACK state machine shared by the client world-frame
    /// receive path and the server client-frame upload path.
    ///
    /// The sequencer is single-threaded: the owner calls Receive/Tick on the game
    /// synchronization context.  It performs no I/O; NACK and resync are
    /// callbacks owned by the endpoint.
    /// </summary>
    internal sealed class FrameSequencer<TFrame> where TFrame : class
    {
        private readonly FrameSequencerPolicy policy;
        private readonly Func<TFrame, long> sequenceOf;
        private readonly Action<TFrame> applyFrame;
        private readonly Func<long, long, Task<FrameSequencerBatch<TFrame>>>? requestFrames;
        private readonly Action<string>? requestResync;
        private readonly Action<object>? applyResync;
        private readonly Action<string>? log;

        private readonly SortedDictionary<long, TFrame> pending = [];
        private long nextExpected;
        private long? gapSinceMs;
        private long lastNackMs;
        private Task<FrameSequencerBatch<TFrame>>? nackTask;
        private bool hasBaseline;

        public FrameSequencer(
            FrameSequencerPolicy policy,
            Func<TFrame, long> sequenceOf,
            Action<TFrame> applyFrame,
            bool hasBaseline = false,
            Func<long, long, Task<FrameSequencerBatch<TFrame>>>? requestFrames = null,
            Action<string>? requestResync = null,
            Action<object>? applyResync = null,
            Action<string>? log = null)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.sequenceOf = sequenceOf ?? throw new ArgumentNullException(nameof(sequenceOf));
            this.applyFrame = applyFrame ?? throw new ArgumentNullException(nameof(applyFrame));
            this.hasBaseline = hasBaseline;
            this.requestFrames = requestFrames;
            this.requestResync = requestResync;
            this.applyResync = applyResync;
            this.log = log;
        }

        public bool HasBaseline => hasBaseline;
        public long LastDelivered { get; private set; } = -1;
        public long NextExpected => nextExpected;
        public int PendingCount => pending.Count;

        public int GapCount { get; private set; }
        public int NackSent { get; private set; }
        public int BundleReceived { get; private set; }
        public int SkippedFrames { get; private set; }
        public int DroppedOld { get; private set; }
        public int PendingOverflow { get; private set; }

        /// <summary>Requests the initial baseline when the stream starts without one.</summary>
        public void Start()
        {
            if (!hasBaseline)
                requestResync?.Invoke("initial");
        }

        /// <summary>Receives one frame; contiguous frames are applied immediately.</summary>
        public void Receive(TFrame frame)
        {
            if (frame == null)
                return;

            long sequence = sequenceOf(frame);

            if (!hasBaseline)
            {
                BufferPending(sequence, frame);
                return;
            }

            if (sequence <= LastDelivered || pending.ContainsKey(sequence))
            {
                DroppedOld++;
                return;
            }

            if (sequence == nextExpected)
            {
                Accept(sequence, frame);
                DrainContiguous();
                return;
            }

            if (pending.Count >= policy.MaxPending)
            {
                PendingOverflow++;
                OnOverflow(sequence, frame);
                return;
            }

            pending[sequence] = frame;
            GapCount++;
        }

        /// <summary>Applies a baseline supplied outside the normal sequence.</summary>
        public void ApplyDirect(TFrame frame)
        {
            if (frame == null)
                return;
            applyFrame(frame);
        }

        /// <summary>Adopts a full baseline sequence; clears pending/gap state.</summary>
        public void AdoptBaseline(long sequence)
        {
            pending.Clear();
            LastDelivered = sequence;
            nextExpected = sequence + 1;
            gapSinceMs = null;
            lastNackMs = 0;
            nackTask = null;
            hasBaseline = true;
        }

        /// <summary>Advances gap/NACK scheduling.  Called once per game tick.</summary>
        public void Tick(long nowMs)
        {
            PollNack();

            if (!hasBaseline)
            {
                // The endpoint throttles repeated resync requests.
                requestResync?.Invoke("baseline retry");
                return;
            }

            if (pending.Count == 0)
            {
                gapSinceMs = null;
                return;
            }

            long missingFrom = nextExpected;
            long missingTo = pending.Keys.Min() - 1;

            if (missingTo < missingFrom)
            {
                DrainContiguous();
                return;
            }

            gapSinceMs ??= nowMs;
            long gapMs = nowMs - gapSinceMs.Value;
            long gapCount = missingTo - missingFrom + 1;

            if (policy.ResyncOnLargeGap
                && (gapCount > policy.ResyncGapFrameCount
                    || gapMs > policy.ResyncGapMs))
            {
                log?.Invoke(
                    $"Frame gap too large: from={missingFrom} to={missingTo}");
                requestResync?.Invoke("gap too large");
                return;
            }

            if (gapMs > policy.GapSkipMs || gapCount > policy.GapSkipCount)
            {
                log?.Invoke(
                    $"Skip frame gap: from={missingFrom} to={missingTo}");
                SkipGap();
                return;
            }

            if (gapMs < policy.NackFirstDelayMs)
                return;
            if (nackTask != null && !nackTask.IsCompleted)
                return;
            if (nowMs - lastNackMs < policy.NackRetryMs)
                return;
            if (requestFrames == null)
                return;

            lastNackMs = nowMs;
            NackSent++;

            long to = Math.Min(
                missingTo, missingFrom + policy.MaxNackRange - 1);
            try
            {
                nackTask = requestFrames(missingFrom, to);
            }
            catch (Exception ex)
            {
                log?.Invoke(
                    $"Frame NACK request failed: {ex.GetBaseException().Message}");
                nackTask = null;
            }
        }

        /// <summary>
        /// Records a frame dropped by an epoch/owner check before it reached the
        /// sequencer.
        /// </summary>
        public void NoteDroppedOld() => DroppedOld++;

        /// <summary>Clears sequencing state but keeps diagnostic counters.</summary>
        public void Reset()
        {
            pending.Clear();
            nextExpected = 0;
            LastDelivered = -1;
            hasBaseline = false;
            gapSinceMs = null;
            lastNackMs = 0;
            nackTask = null;
        }

        private void BufferPending(long sequence, TFrame frame)
        {
            if (pending.Count >= policy.MaxPending)
            {
                PendingOverflow++;
                OnOverflow(sequence, frame);
                return;
            }

            pending[sequence] = frame;
        }

        private void OnOverflow(long sequence, TFrame frame)
        {
            if (policy.OverflowMode == FrameOverflowMode.JumpToNewest)
            {
                pending.Clear();
                nextExpected = sequence;
                Accept(sequence, frame);
                return;
            }

            pending.Clear();
            gapSinceMs = null;
            requestResync?.Invoke("pending overflow");
        }

        private void Accept(long sequence, TFrame frame)
        {
            LastDelivered = sequence;
            nextExpected = sequence + 1;
            gapSinceMs = null;
            applyFrame(frame);
        }

        private void DrainContiguous()
        {
            while (pending.Remove(nextExpected, out var frame))
                Accept(nextExpected, frame);
        }

        private void SkipGap()
        {
            if (pending.Count == 0)
            {
                gapSinceMs = null;
                return;
            }

            if (policy.GapSkipMode == FrameGapSkipMode.Newest)
            {
                SkipToNewest();
                return;
            }

            long earliest = pending.Keys.Min();
            long skipped = earliest - nextExpected;
            if (skipped > 0)
                SkippedFrames += (int)skipped;

            nextExpected = earliest;
            DrainContiguous();
            gapSinceMs = null;
        }

        private void SkipToNewest()
        {
            if (pending.Count == 0)
            {
                gapSinceMs = null;
                return;
            }

            long newest = pending.Keys.Max();
            long skipped = newest - nextExpected;
            if (skipped > 0)
                SkippedFrames += (int)skipped;

            pending.Remove(newest, out var frame);
            pending.Clear();
            nextExpected = newest;

            if (frame != null)
                Accept(newest, frame);

            gapSinceMs = null;
        }

        private void PollNack()
        {
            if (nackTask == null || !nackTask.IsCompleted)
                return;

            var task = nackTask;
            nackTask = null;

            if (task.IsFaulted)
            {
                log?.Invoke($"Frame NACK failed: {task.Exception?.GetBaseException().Message}");
                return;
            }

            if (task.IsCanceled)
                return;

            BundleReceived++;
            var batch = task.Result;

            if (batch.Resync != null)
            {
                applyResync?.Invoke(batch.Resync);
                return;
            }

            if (batch.Unavailable && policy.SkipOnUnavailable)
            {
                SkipToNewest();
                return;
            }

            if (batch.Frames == null)
                return;

            foreach (var frame in batch.Frames)
                Receive(frame);
        }
    }
}
