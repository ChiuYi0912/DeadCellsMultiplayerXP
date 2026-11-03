using dc.en;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Guest;
using DeadCellsMultiplayerX.Client.Heroes.Ghost;
using DeadCellsMultiplayerX.Client.Replication;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Client.MobAI.Modifiers.Zombie
{
    internal sealed class ZombieClientModifier : MobModifierBase<GameZombie>
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ZombieClientModifier");

        private readonly GuestClientSession session;
        private readonly RemoteWorld world;

        public ZombieClientModifier(
            GuestClientSession session,
            RemoteWorld world)
        {
            this.session = session;
            this.world = world;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnAiLocked(_ => true);
            hooks.OnClientUpdate(_ => false);
            hooks.OnFixedUpdate(update);
            hooks.OnClientEvent(OnEvent);
        }

        /// <inheritdoc />
        public override void Attach(GameZombie mob, IMobModifierContext context)
        {
            Logger.Debug(
                "Zombie client modifier attached: netId={NetId} time={Time} local={Local}",
                context.NetId, session.SyncedTimeMs, world.LocalGuid);
        }

        /// <inheritdoc />
        public override void Detach(GameZombie mob)
        {
            Logger.Debug("Zombie client modifier detached: mob={Mob}", mob.GetType().Name);
        }

        public void OnEvent(MobHookContext context, MobModifierEvent evt)
        {
            if (context == null || evt == null) return;
            Game game = session.Game;
            Hero hero = game.hero;

            var ghostmanager = context.ModifierContext.GetService<RemoteGhostManager>();
            if (ghostmanager.TryGet(evt.TargetGuid, out var ghost))
            {
                hero = ghost.Hero;
            }

            context.Mob.setAttackTarget(hero);
            context.Mob.setNemesisTarget(hero);

            var skills = context.Mob.oldSkills;

            hero.maxLife = 9999;
            hero.life = 9999;

            foreach (var item in skills)
            {
                if (item is OldMobSkill mobSkill)
                {
                    if (mobSkill.id.ToString() != evt.SkillId) continue;

                    mobSkill.prepareOnOwnerTarget(true, null);
                }
            }
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
