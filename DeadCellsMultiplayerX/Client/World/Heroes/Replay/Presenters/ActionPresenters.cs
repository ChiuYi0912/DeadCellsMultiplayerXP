namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using Serilog;

internal sealed class SkillActionPresenter : IActionPresenter
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "SkillActionPresenter");

    public HeroActionKind Kind => HeroActionKind.ActiveSkill;

    public bool Present(
        ClientReplayProxyManager manager,
        WorldFrame frame,
        HeroOutcome outcome,
        ClientHeroReplayProxy ghost)
    {
        if (!ghost.Actions.PrepareSkill(outcome.Slot, outcome.Item))
        {
            return false;
        }

        if (!ghost.Actions.ReplaySkill(outcome.Slot))
        {
            Logger.Warning(
                "Skill replay failed: caster={Caster} slot={Slot} item={Item}",
                outcome.CasterGuid, outcome.Slot, outcome.Item?.ItemId);
            return false;
        }

        return true;
    }
}

internal sealed class WeaponActionPresenter : IActionPresenter
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "WeaponActionPresenter");

    public HeroActionKind Kind => HeroActionKind.Weapon;

    public bool Present(
        ClientReplayProxyManager manager,
        WorldFrame frame,
        HeroOutcome outcome,
        ClientHeroReplayProxy ghost)
    {
        if (!ghost.Actions.PrepareWeapon(outcome.Slot, outcome.Item))
        {
            return false;
        }

        if (!ghost.Actions.UseWeapon(outcome.Slot, outcome.Cycle))
        {
            Logger.Warning(
                "Weapon replay failed: caster={Caster} slot={Slot} item={Item}",
                outcome.CasterGuid, outcome.Slot, outcome.Item?.ItemId);
            return false;
        }

        return true;
    }
}

internal sealed class ShieldActionPresenter : IActionPresenter
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ShieldActionPresenter");

    public HeroActionKind Kind => HeroActionKind.Shield;

    public bool Present(
        ClientReplayProxyManager manager,
        WorldFrame frame,
        HeroOutcome outcome,
        ClientHeroReplayProxy ghost)
    {
        if (outcome.Shield == null)
        {
            return false;
        }

        if (!manager.TryGetRemoteHero(outcome.CasterGuid, out var remoteHero))
        {
            return false;
        }

        if (!remoteHero.ApplyShieldVisual(outcome.Shield))
        {
            Logger.Warning(
                "Shield visual replay failed: caster={Caster} " +
                "slot={Slot} block={Block}",
                outcome.CasterGuid, outcome.Slot, outcome.Shield.BlockKind);
            return false;
        }

        if (outcome.Shield.SkillInterrupted)
        {
            manager.InterruptRemoteMobSkill(outcome.Shield.SourceNetId);
        }

        return true;
    }
}
