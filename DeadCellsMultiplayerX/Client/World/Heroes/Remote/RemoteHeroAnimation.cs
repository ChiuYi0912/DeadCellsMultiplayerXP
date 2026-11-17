namespace DeadCellsMultiplayerX.Client.World.Heroes.Remote;

using dc.hl.types;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using ModCore.Utilities;

/// <summary>Single entry point for the visible actor's snapshot and shield animation.</summary>
internal sealed class RemoteHeroAnimation(RemoteHero owner)
{
    private readonly RemoteHero owner = owner;
    private string lastGroup = string.Empty;
    private long lastStartTime = -1;
    private AnimInfo? lastSnapshot;

    public void ApplySnapshot(AnimInfo previous, AnimInfo next, double renderTimeSec)
    {
        if (!this.owner.IsActive)
        {
            return;
        }
        var anim = this.owner.spr?.get_anim();
        if (anim == null)
        {
            return;
        }
        var chosen = AnimStateApplier.Select(previous, next, renderTimeSec);
        if (chosen == null)
        {
            return;
        }
        this.lastSnapshot = chosen;
        if (string.IsNullOrEmpty(chosen.GroupName))
        {
            return;
        }
        if (IsRollGroup(chosen.GroupName) && this.IsRolling())
        {
            this.OnSpriteChanged();
            return;
        }
        if (AnimStateApplier.Play(anim, chosen, this.lastGroup, this.lastStartTime))
        {
            this.lastGroup = chosen.GroupName;
            this.lastStartTime = chosen.StartTime;
        }
    }

    public bool PlayShield(ShieldOutcome outcome, double nowSec)
    {
        if (!this.owner.IsActive || outcome == null)
        {
            return false;
        }
        var anim = this.owner.spr?.get_anim();
        if (anim == null || anim.destroyed)
        {
            return false;
        }
        if (outcome.BlockKind != ShieldBlockKind.None)
        {
            anim.play("blockEnd".AsHaxeString(), null, null);
            this.PlayParryEffects(outcome.BlockKind == ShieldBlockKind.Full);
            return true;
        }
        switch (outcome.Phase)
        {
            case ShieldIntentKind.HoldStart:
                anim.play("block".AsHaxeString(), null, null);
                return true;
            case ShieldIntentKind.HoldEnd:
            case ShieldIntentKind.ParryEnd:
                this.RestoreSnapshot(nowSec);
                return true;
            case ShieldIntentKind.ParryStart:
                anim.play("blockEnd".AsHaxeString(), null, null);
                this.PlayParryEffects(false);
                return true;
            default:
                return false;
        }
    }

    public void OnSpriteChanged()
    {
        this.lastGroup = string.Empty;
        this.lastStartTime = -1;
    }

    private void RestoreSnapshot(double nowSec)
    {
        if (this.lastSnapshot == null)
        {
            return;
        }
        this.OnSpriteChanged();
        this.ApplySnapshot(this.lastSnapshot, this.lastSnapshot, nowSec);
    }

    private void PlayParryEffects(bool withText)
    {
        var level = this.owner._level;
        if (level?.fx == null)
        {
            return;
        }
        if (withText)
        {
            this.owner.popText("Parade !".AsHaxeString(), 65535);
        }
        level.fx.shieldParryStart(this.owner, 4480637, null);
        level.fx.frontShieldHit(this.owner, 6921449);
    }

    private bool IsRolling()
    {
        var affects = this.owner.affects;
        if (affects == null || affects.length <= 3)
        {
            return false;
        }
        var list = AffectCodec.ToObject<ArrayObj>(affects.array[3]);
        return list != null && list.length > 0;
    }

    private static bool IsRollGroup(string name)
        => name is "rolling" or "rollEnd" or "rollIdle" or "rollRun";
}
