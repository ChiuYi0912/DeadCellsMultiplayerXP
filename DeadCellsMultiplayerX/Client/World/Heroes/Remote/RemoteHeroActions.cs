namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Loadout;
using Serilog;

/// <summary>Owns this actor's hidden executor, equipment and action presentation.</summary>
internal sealed class RemoteHeroActions(RemoteHero owner) : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RemoteHeroActions>();
    private readonly RemoteHero owner = owner;
    private RemoteHeroActionProxy? proxy;
    private PlayerLoadout? loadout;
    private uint loadoutVersion;
    private uint? appliedLoadoutVersion;
    private bool disposed;

    internal RemoteHeroActionProxy? Proxy => this.proxy is { IsAlive: true } ? this.proxy : null;

    public void ApplyLoadout(PlayerLoadout? loadout, uint version)
    {
        if (this.disposed)
        {
            return;
        }
        this.loadout = loadout;
        this.loadoutVersion = version;
        this.EnsureProxy();
    }

    public void FollowOwner()
    {
        if (!this.owner.IsActive || this.disposed || this.Proxy is not { } current)
        {
            return;
        }
        current.SetTransform(this.owner.cx, this.owner.cy, this.owner.xr, this.owner.yr,
            this.owner.dir, this.owner.dx, this.owner.dy);
    }

    public bool Present(HeroOutcome outcome)
    {
        if (this.disposed || !this.owner.IsActive || !this.EnsureProxy() || this.Proxy is not { } current)
        {
            return false;
        }
        // Event pose applies only to the hidden executor. Visible motion is snapshot-driven.
        current.SetTransform(outcome.CX, outcome.CY, outcome.XR, outcome.YR,
            outcome.Dir, outcome.DX, outcome.DY);
        current.SetTiers(outcome.BrutalityTier, outcome.SurvivalTier, outcome.TacticTier);

        bool played;
        switch (outcome.Kind)
        {
            case HeroActionKind.ActiveSkill:
                played = current.Actions.PrepareSkill(outcome.Slot, outcome.Item)
                    && current.Actions.ReplaySkill(outcome.Slot);
                break;
            case HeroActionKind.Weapon:
                played = current.Actions.PrepareWeapon(outcome.Slot, outcome.Item)
                    && current.Actions.UseWeapon(outcome.Slot, outcome.Cycle);
                break;
            case HeroActionKind.Shield:
                if (outcome.Shield is not { } shield)
                {
                    return false;
                }
                played = this.owner.Animation.PlayShield(shield, this.owner.SyncedTimeMs / 1000.0);
                if (played && shield.SkillInterrupted)
                {
                    this.owner.InterruptMobSkill(shield.SourceNetId);
                }
                break;
            default:
                Logger.Debug("No presentation for action kind {Kind}: caster={Caster}", outcome.Kind, outcome.CasterGuid);
                return false;
        }
        if (!played)
        {
            Logger.Warning("Remote action presentation failed: kind={Kind} caster={Caster} slot={Slot} item={Item}",
                outcome.Kind, outcome.CasterGuid, outcome.Slot, outcome.Item?.ItemId);
        }
        return played;
    }

    public void ResetProxy()
    {
        var previous = this.proxy;
        this.proxy = null;
        this.appliedLoadoutVersion = null;
        previous?.Dispose();
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }
        this.disposed = true;
        this.loadout = null;
        this.ResetProxy();
    }

    private bool EnsureProxy()
    {
        if (this.disposed || !this.owner.IsActive)
        {
            return false;
        }
        var level = this.owner._level;
        if (level == null)
        {
            return false;
        }
        if (this.proxy?.MatchesLevel(level) != true)
        {
            this.ResetProxy();
            if (this.disposed || !this.owner.IsActive)
            {
                return false;
            }
            var created = RemoteHeroActionProxy.CreateClient(level.game, level, this.owner.Guid);
            // Native initialization can re-enter game code and destroy the owner.
            // Keep ownership local until creation has completed and the actor is still alive.
            if (this.disposed || !this.owner.IsActive)
            {
                created?.Dispose();
                return false;
            }
            this.proxy = created;
        }
        var current = this.proxy;
        if (current == null)
        {
            return false;
        }
        if (this.loadout != null && this.appliedLoadoutVersion != this.loadoutVersion)
        {
            var version = this.loadoutVersion;
            // A newly created proxy must receive equipment even if Spawn.Version is unchanged.
            current.Actions.ApplyLoadout(this.loadout);
            if (this.disposed || !this.owner.IsActive || !ReferenceEquals(current, this.proxy))
            {
                return false;
            }
            this.appliedLoadoutVersion = version;
        }
        return !this.disposed && this.owner.IsActive && this.Proxy != null;
    }
}
