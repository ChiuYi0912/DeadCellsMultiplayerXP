using dc.en;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using ModCore.Utilities;
using HaxeProxy.Runtime;
using Serilog;
using GameShield = dc.en.mob.Shield;

namespace DeadCellsMultiplayerX.Client.MobBehavior.Modifiers.Shield
{
    internal sealed class ShieldClientModifier : MobModifierBase<GameShield>
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ShieldClientModifier");

        private readonly GuestClientSession session;
        private readonly RemoteWorld world;

        public ShieldClientModifier(
            GuestClientSession session,
            RemoteWorld world)
        {
            this.session = session;
            this.world = world;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnClientUpdate(_ => false);
            hooks.OnClientEvent(OnClientEvent);
        }

        /// <inheritdoc />
        public override void Attach(GameShield mob, IMobModifierContext context)
        {
    
        }

        /// <inheritdoc />
        public override void Detach(GameShield mob)
        {
          
        }

        private void OnClientEvent(MobHookContext context, MobModifierEvent evt)
        {
            if (context == null || evt == null)
                return;

            if (evt.Kind != MobModifierEventKind.ActionStarted
                || string.IsNullOrEmpty(evt.SkillId))
                return;

            var mob = context.Mob;
            if (mob == null || mob.destroyed)
                return;

            var hero = ResolveTargetHero(evt.TargetGuid);
            if (hero == null || hero.destroyed)
                return;

            mob.setAttackTarget(hero);
            mob.setNemesisTarget(hero);

            var skills = mob.oldSkills;
            if (skills == null)
                return;

            foreach (var item in skills)
            {
                if (item is not OldMobSkill mobSkill)
                    continue;

                if (mobSkill.id.ToString() != evt.SkillId)
                    continue;

                mobSkill.prepareOnOwnerTarget(true, null);
            }
        }

        private Hero? ResolveTargetHero(string guid)
        {
            if (!string.IsNullOrEmpty(guid)
                && world.Ghosts.TryGet(guid, out var ghost)
                && ghost.Hero != null
                && !ghost.Hero.destroyed)
            {
                ghost.SetPlaceholderVitals();
                return ghost.Hero;
            }

            return session.Game?.hero;
        }
    }
}
