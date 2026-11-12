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
        HeroActionEvent evt,
        ClientHeroReplayProxy ghost)
    {
        if (!ghost.Actions.PrepareSkill(evt.Slot, evt.Item))
        {
            return false;
        }

        if (!ghost.Actions.ReplaySkill(evt.Slot))
        {
            Logger.Warning(
                "Skill replay failed: caster={Caster} slot={Slot} item={Item}",
                evt.CasterGuid, evt.Slot, evt.Item?.ItemId);
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
        HeroActionEvent evt,
        ClientHeroReplayProxy ghost)
    {
        if (!ghost.Actions.PrepareWeapon(evt.Slot, evt.Item))
        {
            return false;
        }

        if (!ghost.Actions.UseWeapon(evt.Slot, evt.Cycle))
        {
            Logger.Warning(
                "Weapon replay failed: caster={Caster} slot={Slot} item={Item}",
                evt.CasterGuid, evt.Slot, evt.Item?.ItemId);
            return false;
        }

        return true;
    }
}

internal sealed class TalismanActionPresenter : IActionPresenter
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "TalismanActionPresenter");

    public HeroActionKind Kind => HeroActionKind.Talisman;

    public bool Present(
        ClientReplayProxyManager manager,
        WorldFrame frame,
        HeroActionEvent evt,
        ClientHeroReplayProxy ghost)
    {
        if (!ghost.Actions.ApplyTalisman(evt.Item))
        {
            Logger.Warning(
                "Talisman replay failed: caster={Caster} item={Item}",
                evt.CasterGuid, evt.Item?.ItemId);
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
        HeroActionEvent evt,
        ClientHeroReplayProxy ghost)
    {
        if (!manager.TryGetRemoteHero(evt.CasterGuid, out var remoteHero))
        {
            return false;
        }

        var elapsedMs = Math.Max(
            0, frame.ServerTimeMs - evt.ServerTimeMs);

        if (!remoteHero.ApplyShieldVisual(
                evt.ShieldKind, evt.Slot, evt.Item,
                evt.FullParry, elapsedMs))
        {
            Logger.Warning(
                "Shield visual replay failed: caster={Caster} " +
                "slot={Slot} kind={Kind}",
                evt.CasterGuid, evt.Slot, evt.ShieldKind);
            return false;
        }

        return true;
    }
}
