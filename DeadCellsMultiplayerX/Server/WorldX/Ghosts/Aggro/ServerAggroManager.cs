using System;
using dc;
using dc.en;
using dc.pr;
using dc.tool.atk;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.Team;
using DeadCellsMultiplayerX.Server.WorldX.Hubs;
using Serilog;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Aggro
{
    /// <summary>
    /// 服务端 Ghost 仇恨协调器
    /// </summary>
    internal sealed class ServerAggroManager
    {
        /// <summary>
        /// 目标巡检间隔
        /// </summary>
        private const long ThinkIntervalMs = 100;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerAggroManager");

        private readonly ServerTeamManager teams;
        private readonly ServerMobRegistry mobs;
        private readonly Func<Game?> gameProvider;

        private long nextThinkMs;
        private int heroSwapDepth;

        /// <summary>
        /// 绑定 Ghost 队伍、mob 注册表与游戏实例
        /// </summary>
        public ServerAggroManager(
            ServerTeamManager teams,
            ServerMobRegistry mobs,
            Func<Game?> gameProvider)
        {
            this.teams = teams;
            this.mobs = mobs;
            this.gameProvider = gameProvider;
        }

        /// <summary>
        /// 在 mob 原版 AI 已经锁定 Ghost 时，临时把 game.hero 指向该 Ghost
        /// 没有目标的 mob 保持原版 AI，不因附近是否存在 Ghost 改变行为
        /// </summary>
        public void RunAsMobHero(Mob mob, Action body) => RunAsHero(ResolveMobHero(mob), body);

        /// <summary>
        /// 在指定 Hero 上下文里执行原版代码，结束后恢复全局 game.hero
        /// 同时把 viewport 临时伪装成覆盖全图：
        /// 某些 mob 需要判断相机距离
        /// </summary>
        public void RunAsHero(Hero? hero, Action body)
        {
            var game = gameProvider();
            if (game == null)
            {
                body();
                return;
            }

            if (heroSwapDepth > 0)
            {
                body();
                return;
            }

            var previous = game.hero;
            if (hero != null && !ReferenceEquals(previous, hero))
                game.hero = hero;

            var viewport = hero?._level?.viewport;
            var map = hero?._level?.map;
            double previousX = 0;
            double previousY = 0;
            int previousWid = 0;
            int previousHei = 0;

            if (viewport != null && map != null)
            {
                previousX = viewport.x;
                previousY = viewport.y;
                previousWid = viewport.wid;
                previousHei = viewport.hei;

                // x/y 是镜头中心，wid/hei 是像素尺寸
                viewport.x = map.wid * 12.0;
                viewport.y = map.hei * 12.0;
                viewport.wid = map.wid * 24;
                viewport.hei = map.hei * 24;
            }

            heroSwapDepth++;
            try
            {
                body();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex,
                    "Mob hero-context failed: hero={Hero}", hero?.type);
            }
            finally
            {
                heroSwapDepth--;

                if (viewport != null)
                {
                    viewport.x = previousX;
                    viewport.y = previousY;
                    viewport.wid = previousWid;
                    viewport.hei = previousHei;
                }

                if (previous != null)
                    game.hero = previous;
            }
        }

        /// <summary>
        /// 当前 mob 原版 AI 已经选中的 Ghost
        /// </summary>
        public Hero? ResolveMobHero(Mob mob)
        {
            if (mob == null || mob.destroyed)
                return null;

            var target = mob.aTarget as Hero ?? mob.nemesisTarget as Hero;
            return IsUsableGhostHero(target) ? target : null;
        }

        /// <summary>
        /// 造成这次攻击的 Ghost
        /// </summary>
        public Hero? ResolveAttackerHero(AttackData? atk)
        {
            var entity = atk?.carrier ?? atk?.source;
            return entity is Hero hero && IsUsableGhostHero(hero)
                ? hero
                : null;
        }

        /// <summary>
        /// 清理跨关卡/死亡/断线的 Ghost 目标
        /// </summary>
        public void Tick(long nowMs)
        {
            if (nowMs < nextThinkMs)
                return;

            nextThinkMs = nowMs + ThinkIntervalMs;

            var game = gameProvider();
            if (game == null || teams.Combatants.Count == 0)
                return;

            foreach (var entry in mobs.Entries)
                ValidateCurrentTarget(entry.Mob, game);
        }

        /// <summary>
        /// 原版 updateThreat 之后调用，只做目标校验，不改变正常选择
        /// </summary>
        public void OnThreatUpdated(Mob mob)
        {
            var game = gameProvider();
            if (game == null || teams.Combatants.Count == 0)
                return;

            ValidateCurrentTarget(mob, game);
        }

        /// <summary>
        /// 阻止 mob 硬锁定服务端假人；Ghost 目标只校验有效性
        /// </summary>
        public bool TryRedirectNemesis(
            Mob mob, Entity? requested, out Entity? target)
        {
            target = requested;

            if (requested == null)
                return false;

            // 先按 Ghost 校验, mob 上下文里 game.hero 会被临时指向 Ghost，
            // 不能再把这个 Ghost 当成服务端假人改掉
            if (teams.TryGet(requested, out var combatant))
            {
                if (IsValidTarget(mob, combatant))
                    return false;

                target = null;
                return true;
            }

            var game = gameProvider();
            if (game?.hero != null && ReferenceEquals(requested, game.hero))
            {
                target = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 校验 mob 当前的 aTarget / nemesisTarget
        /// 清理跨关卡、死亡、断线的 Ghost，以及服务端假人
        /// </summary>
        private void ValidateCurrentTarget(Mob mob, Game game)
        {
            if (mob == null || mob.destroyed || mob.life <= 0)
                return;

            if (mob._level == null || !ReferenceEquals(mob._team, mob._level.teamMob))
                return;

            ValidateNemesis(mob, game);

            var target = mob.aTarget;
            if (target == null)
                return;

            if (teams.TryGet(target, out var combatant))
            {
                if (!IsValidTarget(mob, combatant))
                    ClearTarget(mob, target);

                return;
            }

            // 服务端假人没有玩家控制，不允许成为目标
            if (game.hero != null && ReferenceEquals(target, game.hero))
                ClearTarget(mob, target);
        }

        /// <summary>
        /// 单独校验 nemesisTarget
        /// Ghost 在 mob 上下文里可能就是 game.hero，不能误当成假人清掉
        /// </summary>
        private void ValidateNemesis(Mob mob, Game game)
        {
            var nemesis = mob.nemesisTarget;
            if (nemesis == null)
                return;

            // Ghost 在 mob 上下文里可能就是 game.hero
            // 必须先用 Ghost 身份校验
            if (teams.TryGet(nemesis, out var combatant))
            {
                if (!IsValidTarget(mob, combatant))
                    mob.clearNemesisTarget();

                return;
            }

            if (game.hero != null && ReferenceEquals(nemesis, game.hero))
                mob.clearNemesisTarget();
        }

        /// <summary>
        /// Hero 是否为当前可参与战斗的 Ghost
        /// </summary>
        private bool IsUsableGhostHero(Hero? hero)
            => hero != null
                && !hero.destroyed
                && teams.TryGet(hero, out var combatant)
                && IsValidGhost(combatant);

        /// <summary>
        /// Ghost 是否可作为该 mob 的合法目标（存活、同关卡）
        /// </summary>
        private static bool IsValidTarget(Mob mob, GhostCombatant combatant)
            => IsValidGhost(combatant)
                && ReferenceEquals(combatant.Level, mob._level);

        /// <summary>
        /// Ghost 是否处于可被 mob 选中的战斗状态
        /// </summary>
        private static bool IsValidGhost(GhostCombatant combatant)
            => combatant != null
                && combatant.CombatEnabled
                && !combatant.IsDead
                && combatant.Life > 0
                && combatant.Entity is { destroyed: false };

        /// <summary>
        /// 清除对目标的仇恨并解除 mob 的攻击目标
        /// </summary>
        private static void ClearTarget(Mob mob, Entity target)
        {
            mob.removeAllThreatFor(target);
            mob.setAttackTarget(null);
        }
    }
}
