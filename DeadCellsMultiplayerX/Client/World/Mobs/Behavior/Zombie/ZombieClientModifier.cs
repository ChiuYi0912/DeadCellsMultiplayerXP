using dc.pr;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using ModCore.Utilities;
using Serilog;
using GameZombie = dc.en.mob.Zombie;
namespace DeadCellsMultiplayerX.Client.World.Mobs.Behavior.Zombie
{
    internal sealed class ZombieClientModifier : MobModifierBase<GameZombie>
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ZombieClientModifier");

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnFixedUpdate(update);

            if (hooks is IClientMobHooks clientHooks)
            {
                clientHooks.OnClientEvent(OnEvent);
                clientHooks.OnClientEventHandled(_ => false);
            }
        }


        /// <inheritdoc />
        public override void Attach(GameZombie mob, IMobModifierContext context)
        {

        }

        /// <inheritdoc />
        public override void Detach(GameZombie mob)
        {

        }

        public void OnEvent(MobHookContext context, MobModifierEvent evt)
        {
            // if (context == null || evt == null) return;
            // Game game = session.Game;
            // Hero hero = game.hero;

            // var ghostmanager = context.ModifierContext.GetService<ClientReplayProxyManager>();
            // if (ghostmanager.TryGet(evt.TargetGuid, out var ghost))
            // {
            //     ghost.SetPlaceholderVitals();
            //     hero = ghost.Hero;
            // }

            // context.Mob.setAttackTarget(hero);
            // context.Mob.setNemesisTarget(hero);

            // var skills = context.Mob.oldSkills;

            // foreach (var item in skills)
            // {
            //     if (item is OldMobSkill mobSkill)
            //     {
            //         if (mobSkill.id.ToString() != evt.SkillId) continue;

            //         mobSkill.prepareOnOwnerTarget(true, null);
            //     }
            // }
        }

        public void update(MobHookContext context)
        {
            if (!dc.hxd.Key.Class.isPressed(78))
                return;

            var mob = context.Mob;

            mob.setAttackTarget(Game.Class.ME.hero);
            mob.setNemesisTarget(Game.Class.ME.hero);

            var skills = mob.oldSkills;

            foreach (var item in skills)
            {
                if (item is OldMobSkill mobSkill)
                {
                    if (mobSkill.id.ToString() == "jump")
                    {
                        mobSkill.prepareOnOwnerTarget(true, null);
                    }
                }
            }
        }
    }
}
