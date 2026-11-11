using System;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay.Presenters
{
    internal sealed class SkillActionPresenter : IActionPresenter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "SkillActionPresenter");

        public HeroActionKind Kind => HeroActionKind.ActiveSkill;

        public bool Present(
            RemoteGhostManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientGhostHero ghost)
        {
            if (!ghost.Actions.PrepareSkill(evt.Slot, evt.Item))
                return false;

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
            RemoteGhostManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientGhostHero ghost)
        {
            using (manager.Session.GhostCombat.Begin(frame, evt))
            {
                if (!ghost.Actions.PrepareWeapon(evt.Slot, evt.Item))
                    return false;

                if (!ghost.Actions.UseWeapon(evt.Slot, evt.Cycle))
                {
                    Logger.Warning(
                        "Weapon replay failed: caster={Caster} slot={Slot} item={Item}",
                        evt.CasterGuid, evt.Slot, evt.Item?.ItemId);
                    return false;
                }
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
            RemoteGhostManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientGhostHero ghost)
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
            RemoteGhostManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientGhostHero ghost)
        {
            if (!manager.World.Heros.TryGet(evt.CasterGuid, out var remoteHero))
                return false;

            double elapsedMs = Math.Max(
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
}
