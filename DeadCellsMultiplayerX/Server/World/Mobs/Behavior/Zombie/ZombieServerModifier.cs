using dc;
using dc.tool.skill;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.MobBehavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Mobs.Combat;
using Serilog;
using GameZombie = dc.en.mob.Zombie;

using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.Session;
namespace DeadCellsMultiplayerX.Server.World.Mobs.Behavior.Zombie
{
    /// <summary>
    /// Zombie 服务端修改器占位实现
    /// 当前只验证注册、依赖注入、InitHook 和 hook 分发；
    /// </summary>
    internal sealed class ZombieServerModifier : MobModifierBase<GameZombie>
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ZombieServerModifier");

        private readonly ServerSession session;
        private readonly ServerTeamManager teams;
        private readonly IServerMobAttackRouter attacks;

        public ZombieServerModifier(
            ServerSession session,
            ServerTeamManager teams,
            IServerMobAttackRouter attacks)
        {
            this.session = session;
            this.teams = teams;
            this.attacks = attacks;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            if (hooks is IServerMobHooks serverHooks)
                serverHooks.OnSkillPrepare(SandSkills);
        }

        private void SandSkills(MobHookContext context, OldSkill skill, Entity target, int? data)
        {
            if (!teams.TryGet(target, out var combatant))
                return;

            if (session.Entities.TryGetNetId(
                    context.Mob, out uint netId))
            {
                attacks.SubmitSkill(
                    new MobModifierEvent
                    {
                        Epoch = session.Broadcaster.Epoch,
                        NetId = netId,
                        EntityKind = EntityKind.Mob,
                        Kind = MobModifierEventKind.ActionStarted,
                        TargetGuid = combatant.Guid,
                        SkillId = skill.id.ToString(),
                        SkillRatio = data,
                        ServerTimeMs = session.CurrentTimeStamp,
                        LevelId = combatant.Level?.map?.id?.ToString()
                            ?? string.Empty,
                        SubLevelIndex = combatant.Level
                            ?.GetSubLevelIndex() ?? -1,
                    });
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
    }
}
