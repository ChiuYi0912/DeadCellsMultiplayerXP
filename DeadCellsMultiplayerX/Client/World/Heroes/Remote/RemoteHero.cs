namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.World.Frames;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using ModCore.Storage;
using ModCore.Utilities;
using Serilog;

/// <summary>Visible remote actor and owner of its movement, animation, appearance and action components.</summary>
internal sealed class RemoteHero : KingSkin, IHxbitSerializable<RemoteHero.IHxData>
{
    private static readonly ILogger Logger = Log.ForContext<RemoteHero>();
    private readonly WorldFrameTimeline frames;
    private readonly Func<long> syncedTimeMs;
    private readonly Action<uint> interruptMobSkill;
    private LevelKey targetLevel;
    private bool componentsReleased;
    private bool loggedFirstTick;
    private bool loggedLevelWait;

    public RemoteHero(Level level, WorldFrameTimeline frames, string guid,
        Func<long> syncedTimeMs, Action<uint> interruptMobSkill, int x = 0, int y = 0)
        : base(level, x, y)
    {
        this.frames = frames;
        this.Guid = guid;
        this.syncedTimeMs = syncedTimeMs;
        this.interruptMobSkill = interruptMobSkill;
        this.CreatedLevel = LevelUtils.KeyOf(level);
        this.Motion = new RemoteHeroMotion(this);
        this.Animation = new RemoteHeroAnimation(this);
        this.Appearance = new RemoteHeroAppearance(this);
        this.Actions = new RemoteHeroActions(this);
        this.set_easeSpritePos(false);
        this.hasWineGlass = false;
    }

    public string Guid { get; }
    public LevelKey CreatedLevel { get; }
    public bool IsActive => !this.componentsReleased && !this.destroyed;
    public long SyncedTimeMs => this.syncedTimeMs();
    public RemoteHeroMotion Motion { get; }
    public RemoteHeroAnimation Animation { get; }
    public RemoteHeroAppearance Appearance { get; }
    public RemoteHeroActions Actions { get; }

    public override void init()
    {
        base.init();
        this.hasRepelling = false;
        this.hasGravity = false;
        this.gravity = 0;
        this.collisionMode = new CollisionMode.None();
        this.frict = 0;
    }

    public override void initGfx()
    {
        base.initGfx();
        this.spr.get_anim().removeAllStateAnims();
        this.createConfLight("Hero".AsHaxeString());

        this.Animation?.OnSpriteChanged();
        this.Appearance?.Invalidate();
    }

    public void ApplySpawn(HeroSpawn spawn)
    {
        if (!this.IsActive)
        {
            return;
        }
        if (this.Appearance.Apply(spawn))
        {
            this.Animation.OnSpriteChanged();
        }
        this.Actions.ApplyLoadout(spawn.Loadout, spawn.Version);
    }

    public void SnapToState(HeroDynamic state)
    {
        this.targetLevel = LevelKey.Make(state.LevelId, state.SubLevelIndex);
        this.Motion.SnapTo(state.Pos);
    }

    public override void fixedUpdate()
    {
        if (!this.IsActive)
        {
            return;
        }
        var nowSec = this.SyncedTimeMs / 1000.0;
        if (this.frames.TryGetHeroBracket(this.Guid, nowSec,
                out var previous, out var next, out var alpha, out var renderTimeSec))
        {
            if (LevelKey.Make(previous.LevelId, previous.SubLevelIndex) != this.targetLevel)
            {
                if (!this.loggedLevelWait)
                {
                    this.loggedLevelWait = true;
                    Logger.Information("RemoteHero {Guid} waiting for level {Target}: sample={Level}#{SubLevel}",
                        this.Guid, this.targetLevel, previous.LevelId, previous.SubLevelIndex);
                }
                base.fixedUpdate();
                return;
            }
            if (!this.loggedFirstTick)
            {
                this.loggedFirstTick = true;
                Logger.Information("RemoteHero {Guid} first tick: {Level}#{SubLevel}",
                    this.Guid, next.LevelId, next.SubLevelIndex);
            }
            this.Motion.Apply(previous.Pos, next.Pos, alpha);
            AffectCodec.Apply(this, previous.Affects, nowSec, this.cd?.baseFps ?? 60.0);
            this.Animation.ApplySnapshot(previous.Anim, next.Anim, renderTimeSec);
        }
        base.fixedUpdate();
    }

    public void Present(HeroOutcome outcome)
    {
        if (!this.IsActive || outcome.LevelId != this.CreatedLevel.LevelId
            || outcome.SubLevelIndex != this.CreatedLevel.SubLevelIndex)
        {
            return;
        }
        this.Actions.Present(outcome);
    }

    internal void InterruptMobSkill(uint netId) => this.interruptMobSkill(netId);

    public override void destroy()
    {
        try
        {
            this.ReleaseComponents();
        }
        finally
        {
            base.destroy();
        }
    }

    public override void dispose()
    {
        try
        {
            this.ReleaseComponents();
        }
        finally
        {
            base.dispose();
        }
    }

    private void ReleaseComponents()
    {
        if (this.componentsReleased)
        {
            return;
        }
        this.componentsReleased = true;
        // The action component owns every disposable/native presentation resource.
        // The other components hold only actor-local playback/application state.
        try
        {
            this.Actions?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "RemoteHero component cleanup failed: {Guid}", this.Guid);
        }
    }

    public override bool _isOnScreen() => true;
    public sealed class IHxData { }
    IHxData IHxbitSerializable<IHxData>.GetData() => new();
    void IHxbitSerializable<IHxData>.SetData(IHxData data) { }
}
