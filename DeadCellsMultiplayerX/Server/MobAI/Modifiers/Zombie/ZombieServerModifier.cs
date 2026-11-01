using dc;
using dc.en;
using dc.tool.skill;
using DeadCellsMultiplayerX.Common.MobAI.Events;
using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
using DeadCellsMultiplayerX.Utils;
using Serilog;
using GameZombie = dc.en.mob.Zombie;

namespace DeadCellsMultiplayerX.Server.MobAI.Modifiers.Zombie
{
    /// <summary>
    /// Zombie 服务端修改器占位实现。
    /// 当前只验证注册、依赖注入、InitHook 和 hook 分发；
    /// 不拦截原版 AI，也不改变移动/命中。
    /// </summary>
    internal sealed class ZombieServerModifier : MobModifierBase<GameZombie>
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ZombieServerModifier");

        private readonly ServerSession session;
        private readonly ServerTeamManager teams;

        public ZombieServerModifier(
            ServerSession session,
            ServerTeamManager teams)
        {
            this.session = session;
            this.teams = teams;
        }

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            hooks.OnSkillPrepare(SandSkills);
        }

        private void SandSkills(MobHookContext context, OldSkill skill, GhostCombatant target, int? data)
        {
            if (session.MobRegistry.TryGetNetId(
                    context.Mob, out uint netId))
            {
                session.Ghosts.MobAttacks.SubmitHit(
                    new MobModifierEvent
                    {
                        Epoch = session.Broadcaster.Epoch,
                        MobNetId = netId,
                        Kind = MobModifierEventKind.ActionStarted,
                        TargetGuid = target.Guid,
                        SkillId = skill.id.ToString(),
                        SkillRatio = data,
                        ServerTimeMs = session.CurrentTimeStamp,
                        LevelId = target.Level?.map?.id?.ToString()
                            ?? string.Empty,
                        SubLevelIndex = target.Level
                            ?.GetSubLevelIndex() ?? -1,
                    });
            }
        }

        /// <inheritdoc />
        public override void Attach(GameZombie mob, IMobModifierContext context)
        {
            Logger.Debug(
                "Zombie modifier attached: netId={NetId} time={Time} ghostTargets={Targets}",
                context.NetId, session.CurrentTimeStamp,
                teams.Combatants.Count);
        }

        /// <inheritdoc />
        public override void Detach(GameZombie mob)
        {
            Logger.Debug("Zombie modifier detached: mob={Mob}", mob.GetType().Name);
        }
    }
}
