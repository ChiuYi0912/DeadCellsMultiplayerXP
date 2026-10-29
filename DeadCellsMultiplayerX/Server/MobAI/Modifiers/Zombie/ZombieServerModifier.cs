using DeadCellsMultiplayerX.Common.MobAI.Modifiers;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.World.Ghosts.Team;
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

        /// <inheritdoc />
        public override void InitHook(IMobHookRegistry hooks)
        {
            // 占位：未来在这里实现完全自定义的移动/决策。
            //hooks.OnBehaviourAi(_ => true);
            // hooks.OnFixedUpdateHandled(_ => false);
            // hooks.OnMobDied(_ => { });
            hooks.OnAiLocked(h => false);
            hooks.OnFixedUpdateHandled(h => true);
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
