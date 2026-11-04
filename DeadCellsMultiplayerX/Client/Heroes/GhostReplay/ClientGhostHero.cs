using System;
using dc;
using dc.en.hero;
using dc.hl.types;
using dc.pr;
using dc.tool;
using HaxeProxy.Runtime;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay
{
    /// <summary>
    /// 客户端 Ghost：用于技能/武器重放的隐形 Hero。
    /// 只存在于本地关卡，不参与存档与权威结算。
    /// </summary>
    internal sealed class ClientGhostHero : IGhostEntity
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ClientGhostHero");

        private bool disposed;

        private ClientGhostHero(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new ClientGhostActions(hero, ownerGuid);
            LevelKey = GhostLevelKey.From(hero._level);
        }

        public Beheaded Hero { get; }

        public string OwnerGuid { get; }

        public ClientGhostActions Actions { get; }

        public bool IsAlive => Hero != null && !Hero.destroyed;

        public GhostLevelKey LevelKey { get; private set; }

        /// <summary>
        /// Mob 技能需要目标存活时使用的占位生命，避免被本地伤害打死。
        /// </summary>
        public void SetPlaceholderVitals(int life = 9999)
        {
            if (disposed || Hero.destroyed) return;
            Hero.maxLife = life;
            Hero.life = life;
        }

        public bool MatchesLevel(Level? level)
        {
            if (level == null || !IsAlive) return false;
            if (!ReferenceEquals(Hero._level, level)) return false;
            return LevelKey.Matches(level);
        }

        public void SetTransform(
            int cx, int cy, double xr, double yr, int dir, double dx, double dy)
        {
            Hero.setPosCase(cx, cy, xr, yr);
            Hero.dir = dir;
            Hero.dx = dx;
            Hero.dy = dy;
        }

        public void SetTiers(int brutality, int survival, int tactic)
        {
            Hero.setBrutalityTier(brutality);
            Hero.setSurvivalTier(survival);
            Hero.setTacticTier(tactic);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            Actions.Dispose();

            if (Hero._team != null)
            {
                if (Hero._targetable)
                    Hero.set_targetable(false);

                Hero._team.removeMember(Hero);
            }

            // 引擎已销毁/回收的 Hero 不再二次 dispose
            if (Hero._level != null && !Hero.destroyed)
            {
                Hero.destroy();
                Hero.dispose();
                Hero._level = null;
            }

            Hero._team = null;
            Logger.Information("ClientGhostHero disposed: owner={Owner}", OwnerGuid);
        }

        private static void EnsureOpposing(Team? a, Team? b)
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
                if (ReferenceEquals(AffectCodec.ToObject<Team>(opposing.getDyn(i)), b))
                    return;
            }

            opposing.push(b);
        }

        public static ClientGhostHero? CreateClient(
            Game? game,
            Level? level,
            string ownerGuid)
        {
            if (game == null || level == null) return null;

            var reference = game.hero;
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

            return new ClientGhostHero(hero, ownerGuid);
        }
    }
}
