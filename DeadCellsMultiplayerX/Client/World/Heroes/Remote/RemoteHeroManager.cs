namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc.en;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>Reconciles remote actors; each actor owns all of its presentation resources.</summary>
internal sealed class RemoteHeroManager(GuestGameSession session, LocalHeroState localHero,
    string localGuid, Action<uint> interruptMobSkill) : DisposableEventReceiver, IOnLocalHeroInitDone
{
    private readonly Dictionary<string, RemoteHero> heroes = [];
    private readonly HashSet<string> missingSpawnLogged = [];
    private int lastEpoch = int.MinValue;
    private bool clearing;
#if DEBUG
    private readonly Dictionary<string, string> mismatchLogged = [];
    private string lastHeroSetSignature = string.Empty;
#endif

    public event Action<string>? HeroRemoved;
    public IEnumerable<RemoteHero> All => this.heroes.Values;
    public int ActionProxyCount => this.heroes.Values.Count(hero => hero.Actions.Proxy != null);
    private string LocalLevelId => localHero.CurrentLevelId;
    private int LocalSubLevel => localHero.SubLevelIndex;
    private LevelKey LocalLevel => LevelKey.Make(this.LocalLevelId, this.LocalSubLevel);

    public void ApplyFrame(WorldFrame frame, WorldFrameTimeline frames)
    {
        if (this.IsDisposed || this.clearing || frame == null)
        {
            return;
        }
#if DEBUG
        this.LogHeroSet(frame);
#endif
        if (frame.Epoch != this.lastEpoch)
        {
            this.lastEpoch = frame.Epoch;
            // A proxy belongs to its actor, including invalidation at epoch changes.
            foreach (var hero in this.heroes.Values.ToArray())
            {
                try
                {
                    hero.Actions.ResetProxy();
                }
                catch (Exception ex)
                {
                    this.Logger.Warning(ex, "Remote action proxy epoch cleanup failed");
                }
            }
        }

        if (frame.Spawned != null)
        {
            foreach (var spawn in frame.Spawned)
            {
                if (spawn != null && this.TryGet(spawn.GUID, out var existing)
                    && existing.CreatedLevel == this.LocalLevel)
                {
                    existing.ApplySpawn(spawn);
                }
            }
        }

        HashSet<string> seen = [];
        if (frame.Heroes != null)
        {
            foreach (var state in frame.Heroes)
            {
                if (state == null || state.GUID == localGuid)
                {
                    continue;
                }
                if (LevelKey.Make(state.LevelId, state.SubLevelIndex) != this.LocalLevel)
                {
#if DEBUG
                    this.LogMismatch(state);
#endif
                    this.Despawn(state.GUID);
                    continue;
                }
#if DEBUG
                this.mismatchLogged.Remove(state.GUID);
#endif
                seen.Add(state.GUID);
                this.heroes.TryGetValue(state.GUID, out var hero);
                if (hero != null && (!hero.IsActive || hero.CreatedLevel != this.LocalLevel))
                {
                    this.Despawn(state.GUID);
                    hero = null;
                }
                if (!frames.TryGetSpawn(state.GUID, out var spawn))
                {
                    if (hero == null && this.missingSpawnLogged.Add(state.GUID))
                    {
                        this.Logger.Warning("No spawn for remote hero {Guid} on level {Level}#{SubLevel}",
                            state.GUID, state.LevelId, state.SubLevelIndex);
                    }
                    continue;
                }
                if (hero == null)
                {
                    this.CreateHero(spawn, frames, state);
                    this.missingSpawnLogged.Remove(state.GUID);
                }
                else
                {
                    hero.ApplySpawn(spawn);
                }
            }
        }
        if (frame.Despawned != null)
        {
            foreach (var guid in frame.Despawned)
            {
                if (!string.IsNullOrEmpty(guid) && guid != localGuid)
                {
                    this.Despawn(guid);
                }
            }
        }
        foreach (var guid in this.heroes.Keys.Where(guid => !seen.Contains(guid)).ToArray())
        {
            this.Despawn(guid);
        }
    }

    public void UpdateActionProxies()
    {
        if (this.IsDisposed || this.clearing)
        {
            return;
        }
        var level = GuestGameSession.Game?.curLevel;
        if (level == null)
        {
            return;
        }
        foreach (var hero in this.heroes.Values)
        {
            if (ReferenceEquals(hero._level, level))
            {
                hero.Actions.FollowOwner();
            }
        }
    }

    public void Present(HeroOutcome outcome)
    {
        var level = GuestGameSession.Game?.curLevel;
        if (level != null && this.TryGet(outcome.CasterGuid, out var hero)
            && ReferenceEquals(hero._level, level))
        {
            hero.Present(outcome);
        }
    }

    public bool TryGet(string guid, out RemoteHero hero)
    {
        if (!this.IsDisposed && this.heroes.TryGetValue(guid, out hero!) && hero.IsActive)
        {
            return true;
        }
        hero = null!;
        return false;
    }

    public bool TryGetActionProxy(string guid, out RemoteHeroActionProxy proxy)
    {
        if (this.TryGet(guid, out var hero) && hero.Actions.Proxy is { } current)
        {
            proxy = current;
            return true;
        }
        proxy = null!;
        return false;
    }

    public void Despawn(string guid)
    {
        if (!this.heroes.Remove(guid, out var hero))
        {
            return;
        }
        this.Logger.Information("Despawn remote hero {Guid}", guid);
        try
        {
            this.HeroRemoved?.Invoke(guid);
        }
        finally
        {
            hero.destroy();
        }
    }

    private void CreateHero(HeroSpawn spawn, WorldFrameTimeline frames, HeroDynamic state)
    {
        if (this.IsDisposed || this.clearing)
        {
            return;
        }
        var level = GuestGameSession.Game.curLevel;
        var hero = new RemoteHero(level, frames, spawn.GUID, () => session.SyncedTimeMs, interruptMobSkill);
        try
        {
            hero.init();
            hero.ApplySpawn(spawn);
            hero.SnapToState(state);
            hero.Actions.FollowOwner();
            // Native initialization can stop the world before this actor is published.
            if (this.IsDisposed || this.clearing || !hero.IsActive)
            {
                hero.destroy();
                return;
            }
            this.heroes.Add(spawn.GUID, hero);
            this.Logger.Information("Created remote hero {Guid}: level={Level} position=({X},{Y})",
                spawn.GUID, hero.CreatedLevel, hero.cx + hero.xr, hero.cy + hero.yr);
        }
        catch (Exception initializationError)
        {
            try
            {
                hero.destroy();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(initializationError, cleanupError);
            }
            throw;
        }
    }

    private void ClearHeroes()
    {
        if (this.clearing)
        {
            return;
        }
        this.clearing = true;
        var previous = this.heroes.ToArray();
        this.heroes.Clear();
        this.missingSpawnLogged.Clear();
        try
        {
            foreach (var (guid, hero) in previous)
            {
                try
                {
                    try
                    {
                        this.HeroRemoved?.Invoke(guid);
                    }
                    finally
                    {
                        hero.destroy();
                    }
                }
                catch (Exception ex)
                {
                    this.Logger.Warning(ex, "Remote hero cleanup failed: {Guid}", guid);
                }
            }
        }
        finally
        {
            this.clearing = false;
        }
    }

    protected override void MyDispose()
    {
        base.MyDispose();
        this.ClearHeroes();
        this.HeroRemoved = null;
    }

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        if (this.IsDisposed || !ReferenceEquals(hero, GuestGameSession.Game?.hero))
        {
            return;
        }
        this.ClearHeroes();
    }

#if DEBUG
    private void LogMismatch(HeroDynamic state)
    {
        var key = $"{state.LevelId}#{state.SubLevelIndex}|{this.LocalLevelId}#{this.LocalSubLevel}";
        if (this.mismatchLogged.TryGetValue(state.GUID, out var last) && last == key)
        {
            return;
        }

        this.mismatchLogged[state.GUID] = key;
        this.Logger.Information(
            "Remote hero level mismatch: guid={Guid} state={StateLevel}#{StateSub} local={LocalLevel}#{LocalSub}",
            state.GUID, state.LevelId, state.SubLevelIndex, this.LocalLevelId, this.LocalSubLevel);
    }


    private void LogHeroSet(WorldFrame frame)
    {
        if (frame.Heroes == null)
        {
            return;
        }

        var sb = new System.Text.StringBuilder();
        foreach (var hero in frame.Heroes)
        {
            if (hero == null)
            {
                continue;
            }

            if (sb.Length > 0)
            {
                sb.Append(';');
            }

            sb.Append(hero.GUID).Append('@')
              .Append(hero.LevelId).Append('#')
              .Append(hero.SubLevelIndex);
        }

        var signature = sb.ToString();
        if (signature == this.lastHeroSetSignature)
        {
            return;
        }

        this.lastHeroSetSignature = signature;
        this.Logger.Information("World frame heroes: {Heroes}", signature);
    }
#endif

}
