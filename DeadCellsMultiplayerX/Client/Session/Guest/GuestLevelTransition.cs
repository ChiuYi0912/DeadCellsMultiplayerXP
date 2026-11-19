namespace DeadCellsMultiplayerX.Client.Session.Guest;

internal enum GuestLevelPhase
{
    InitialLoading,
    Ready,
    WaitingForServer,
    LoadingNative,
    SyncingWorld,
}

/// <summary>
/// Game-thread rules for one guest's level lifecycle. Native calls and world
/// ownership stay in the session; this object tracks epochs and callback identities.
/// </summary>
internal sealed class GuestLevelTransition
{
    private object? nativeTransition;
    private int commandedEpoch = -1;

    public GuestLevelPhase Phase { get; private set; } = GuestLevelPhase.InitialLoading;
    public long Generation { get; private set; } = 1;
    public bool IsStopped { get; private set; }
    public string? TargetLevelId { get; private set; }
    public int MinimumEpochExclusive { get; private set; } = -1;
    public int CandidateEpoch { get; private set; } = -1;
    public int AcceptedEpoch { get; private set; } = -1;
    public int ExpectedEpoch { get; private set; } = -1;

    public bool CanTickWorld => !this.IsStopped && this.Phase is
        GuestLevelPhase.Ready or GuestLevelPhase.WaitingForServer or GuestLevelPhase.SyncingWorld;

    public bool CanUploadFrames => !this.IsStopped
        && (this.Phase is GuestLevelPhase.Ready or GuestLevelPhase.WaitingForServer
            || (this.Phase == GuestLevelPhase.SyncingWorld
                && this.AcceptedEpoch > this.MinimumEpochExclusive
                && this.AcceptedEpoch >= this.CandidateEpoch));

    public bool CanRecordActions => !this.IsStopped && this.Phase == GuestLevelPhase.Ready;

    public bool CanApplyWorldEpoch(int epoch)
    {
        if (this.IsStopped || epoch < 0)
        {
            return false;
        }
        return this.Phase switch
        {
            GuestLevelPhase.Ready => epoch == this.AcceptedEpoch,
            GuestLevelPhase.WaitingForServer => epoch == this.MinimumEpochExclusive,
            GuestLevelPhase.SyncingWorld => (this.ExpectedEpoch < 0 || epoch == this.ExpectedEpoch)
                && epoch > this.MinimumEpochExclusive
                && epoch >= this.CandidateEpoch && epoch >= this.AcceptedEpoch,
            _ => false,
        };
    }

    /// <summary>Records metadata only; observing a future epoch never starts a world or permits upload.</summary>
    public bool ObserveServerEpoch(int epoch)
    {
        if (this.ExpectedEpoch >= 0 && (this.Phase is GuestLevelPhase.LoadingNative or GuestLevelPhase.SyncingWorld)
            && epoch != this.ExpectedEpoch)
        {
            return false;
        }

        var floor = Math.Max(this.MinimumEpochExclusive, this.AcceptedEpoch);
        if (this.IsStopped || epoch < 0 || epoch <= floor || epoch <= this.CandidateEpoch)
        {
            return false;
        }
        this.CandidateEpoch = epoch;
        return true;
    }

    public bool TryCompleteInitialLoad(long generation, string levelId)
    {
        if (!this.IsCurrent(generation) || this.Phase != GuestLevelPhase.InitialLoading
            || string.IsNullOrEmpty(levelId))
        {
            return false;
        }
        this.TargetLevelId = levelId;
        this.Phase = GuestLevelPhase.SyncingWorld;
        return true;
    }

    public bool TryBeginWaitingForServer(string levelId, int currentEpoch, out long generation)
    {
        generation = this.Generation;
        if (this.IsStopped || this.Phase != GuestLevelPhase.Ready
            || currentEpoch != this.AcceptedEpoch || string.IsNullOrEmpty(levelId))
        {
            return false;
        }
        this.ExpectedEpoch = -1;
        this.BeginTransition(currentEpoch);
        this.TargetLevelId = levelId;
        this.Phase = GuestLevelPhase.WaitingForServer;
        generation = this.Generation;
        return true;
    }

    public bool TryAuthorizeServerTransition(int fromEpoch, int epoch, int currentEpoch)
    {
        if (this.IsStopped || fromEpoch < 1 || epoch <= fromEpoch || epoch <= this.commandedEpoch
            || fromEpoch != currentEpoch)
        {
            return false;
        }

        if (this.Phase == GuestLevelPhase.Ready && fromEpoch != this.AcceptedEpoch)
        {
            return false;
        }

        if (this.Phase == GuestLevelPhase.WaitingForServer && fromEpoch != this.MinimumEpochExclusive)
        {
            return false;
        }

        if (this.Phase is not (GuestLevelPhase.Ready or GuestLevelPhase.WaitingForServer))
        {
            return false;
        }

        this.commandedEpoch = this.ExpectedEpoch = epoch;
        this.CandidateEpoch = epoch;
        return true;
    }

    public bool TryBeginNativeTransition(
        string levelId, int currentEpoch, object transition, out long generation)
    {
        generation = this.Generation;
        if (this.IsStopped || transition == null || string.IsNullOrEmpty(levelId))
        {
            return false;
        }
        if (this.Phase == GuestLevelPhase.Ready && currentEpoch == this.AcceptedEpoch)
        {
            this.BeginTransition(currentEpoch);
        }
        else if (this.Phase != GuestLevelPhase.WaitingForServer || currentEpoch != this.MinimumEpochExclusive)
        {
            return false;
        }

        // The server's selection may differ from this player's vote.
        this.TargetLevelId = levelId;
        this.nativeTransition = transition;
        this.Phase = GuestLevelPhase.LoadingNative;
        generation = this.Generation;
        return true;
    }

    public bool TryCompleteNativeLoad(long generation, object transition, string levelId)
    {
        if (!this.IsCurrent(generation) || this.Phase != GuestLevelPhase.LoadingNative
            || !ReferenceEquals(transition, this.nativeTransition)
            || !string.Equals(levelId, this.TargetLevelId, StringComparison.Ordinal))
        {
            return false;
        }
        this.nativeTransition = null;
        this.Phase = GuestLevelPhase.SyncingWorld;
        return true;
    }

    /// <summary>Called only after the receiver accepts an epoch, before upload/command state advances.</summary>
    public bool TryAcceptWorldEpoch(long generation, int epoch)
    {
        if (!this.IsCurrent(generation) || !this.CanApplyWorldEpoch(epoch))
        {
            return false;
        }
        if (this.Phase == GuestLevelPhase.SyncingWorld)
        {
            this.AcceptedEpoch = epoch;
        }
        return true;
    }

    public bool TryCompleteBaseline(long generation, int epoch)
    {
        if (!this.IsCurrent(generation) || this.Phase != GuestLevelPhase.SyncingWorld
            || epoch != this.AcceptedEpoch || epoch <= this.MinimumEpochExclusive
            || epoch < this.CandidateEpoch)
        {
            return false;
        }
        this.Phase = GuestLevelPhase.Ready;
        this.CandidateEpoch = -1;
        return true;
    }

    public void Stop()
    {
        if (this.IsStopped)
        {
            return;
        }
        this.IsStopped = true;
        this.Generation++;
        this.TargetLevelId = null;
        this.nativeTransition = null;
    }

    private bool IsCurrent(long generation) => !this.IsStopped && generation == this.Generation;

    private void BeginTransition(int epoch)
    {
        this.Generation++;
        this.MinimumEpochExclusive = epoch;
        this.AcceptedEpoch = -1;
        if (this.CandidateEpoch <= epoch)
        {
            this.CandidateEpoch = -1;
        }
        this.nativeTransition = null;
    }
}
