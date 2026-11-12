using dc.en;
using dc.en.hero;
using dc.hl.types;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes
{
    /// <summary>
    /// Creates and tears down the hidden Beheaded used by both the client replay
    /// proxy and the server combat proxy.  The two roles differ only in sprite
    /// setup and visibility; team/opposing setup is shared.
    /// </summary>
    internal static class HeroProxyFactory
    {
        /// <summary>
        /// Client replay proxy: visible=false, sprite copied from the local hero.
        /// </summary>
        public static Beheaded? CreateClient(
            Game? game,
            Level? level,
            Hero? reference)
        {
            if (game == null || level == null)
                return null;

            if (reference == null || reference.spr == null)
                return null;

            var hero = new Beheaded(game);
            hero.init();
            hero._level = level;
            hero._targetable = false;
            hero._team = level.teamHero;

            hero.createAttackSource();
            hero.createAttackTarget();

            hero.life = 1000;
            hero.maxLife = 1000;

            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);

            hero.initSprite(
                reference.spr.lib,
                reference.spr.groupName,
                null, null, null, false, null, null);

            // hero.init() 时 _level 还是 null，Hero.initGfx 里的
            // heroHead.init(level) 绑定到了 null。这里用真实关卡重新初始化，
            // 并把 head 的 owner 指向 Ghost 自己，Dispose 才能正常执行。
            hero.heroHead.init(level, null, Ref<bool>.In(false));
            hero.heroHead.hero = hero;
            hero.setHeadDepth(dc.Const.Class.DP_ROOM_MAIN_HERO);

            hero.visible = false;
            hero.spr.visible = false;

            return hero;
        }

        /// <summary>
        /// Server combat proxy: visible and targetable via later SetCombatEnabled.
        /// </summary>
        public static Beheaded? CreateServer(
            Game? game,
            Level? level,
            int cx,
            int cy,
            int life)
        {
            if (game == null || level == null)
                return null;

            var hero = new Beheaded(game);
            hero._targetable = false;
            hero.wakeup(level, cx, cy);

            hero.setHeadDepth(dc.Const.Class.DP_ROOM_MAIN_HERO);

            hero.life = life;
            hero.maxLife = life;
            hero.visible = true;
            hero.spr.visible = true;

            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);

            return hero;
        }

        public static void EnsureOpposing(Team? a, Team? b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return;

            var opposing = a.opposingTeams;
            if (opposing == null)
            {
                opposing = (ArrayObj)ArrayUtils.CreateDyn().array;
                a.opposingTeams = opposing;
            }

            for (int i = 0; i < opposing.length; i++)
            {
                if (ReferenceEquals(
                        AffectCodec.ToObject<Team>(opposing.getDyn(i)), b))
                    return;
            }

            opposing.push(b);
        }

        /// <summary>Client proxy disposal: clears the level reference directly.</summary>
        public static void DisposeClient(Beheaded hero)
        {
            if (hero == null) return;

            if (hero._team != null)
            {
                if (hero._targetable)
                    hero.set_targetable(false);

                hero._team.removeMember(hero);
            }

            if (hero._level != null && !hero.destroyed)
            {
                hero.destroy();
                hero.dispose();
                hero._level = null;
            }

            hero._team = null;
        }

        /// <summary>Server proxy disposal: uses set_level(null).</summary>
        public static void DisposeServer(Beheaded hero)
        {
            if (hero == null) return;

            if (hero._team != null)
            {
                if (hero._targetable)
                    hero.set_targetable(false);

                hero._team.removeMember(hero);
            }

            if (hero._level != null && !hero.destroyed)
            {
                hero.destroy();
                hero.dispose();
                hero.set_level(null);
            }

            hero._team = null;
        }
    }
}
