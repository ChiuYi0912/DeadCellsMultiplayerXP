using System;
using dc;
using dc.en.hero;
using dc.hl.types;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// 隐形Hero的生命周期壳
    /// </summary>
    internal sealed class GhostHero : IDisposable
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostHero");

        /// <summary>
        /// 客户端在服务端的实体
        /// </summary>
        public Beheaded Hero { get; }

        /// <summary>
        /// 客户端id
        /// </summary>
        public string OwnerGuid { get; }

        /// <summary>
        /// 生成时的关卡
        /// </summary>
        public string LevelId { get; private set; } = string.Empty;

        /// <summary>
        /// 生成时的关卡id
        /// </summary>
        public int SubLevelIndex { get; private set; } = -1;

        /// <summary>
        /// 用于控制此Hero
        /// </summary>
        public GhostActions Actions { get; }

        private GhostHero(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new GhostActions(hero, ownerGuid);
        }

        /// <summary>
        /// 预留给后续武器技能状态tick
        /// </summary>
        public void Tick(double dt)
        {
            Actions.Tick(dt);
        }

        /// <summary>
        /// 设置仇恨
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
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

        public void SyncLevel()
        {
            LevelId = Hero._level.map.id.ToString() ?? string.Empty;
            SubLevelIndex = Hero._level.GetSubLevelIndex();
        }

        /// <summary>
        /// 判断 GhostHero 是否仍在指定关卡和子关卡
        /// </summary>
        public bool MatchesLevel(Level level)
        {
            if (level == null) return false;
            if (!ReferenceEquals(Hero._level, level)) return false;
            return SubLevelIndex == level.GetSubLevelIndex();
        }

        /// <summary>
        /// 位移至目标点,用于精准释放技能
        /// </summary>
        public void SetTransform(int cx, int cy, double xr, double yr, int dir, double dx, double dy)
        {
            Hero.cx = cx;
            Hero.cy = cy;
            Hero.xr = xr;
            Hero.yr = yr;
            Hero.dir = dir;
            Hero.dx = dx;
            Hero.dy = dy;
        }

        /// <summary>
        /// 写入三系等级用于还原服务端伤害缩放
        /// </summary>
        public void SetTiers(int brutality, int survival, int tactic)
        {
            Hero.setBrutalityTier(brutality);
            Hero.setSurvivalTier(survival);
            Hero.setTacticTier(tactic);
        }

        public void Dispose()
        {
            Actions.Dispose();

            Hero._team = null;
            Hero._level = null;

            Logger.Information("GhostHero disposed: owner={Owner}", OwnerGuid);
        }

        /// <summary>
        /// 创建一个未注册的隐形Hero
        /// </summary>
        public static GhostHero? Create(Game? game, Level? level, string ownerGuid, bool isserver = false)
        {
            if (game == null || level == null) return null;

            var hero = new Beheaded(game);
            hero.init();
            hero._level = level; // 使用set_level/set_team会注册实体 存档与队伍成员
            hero._targetable = false;
            hero._team = level.teamHero;
            hero.life = 1000;
            hero.maxLife = 1000;

            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);

            var ghost = new GhostHero(hero, ownerGuid);
            ghost.SyncLevel();

            var reference = game.hero;
            hero.initSprite(reference.spr.lib, reference.spr.groupName, null, null, null, false, null, null);

            hero.visible = isserver;
            hero.spr.visible = isserver;

            Logger.Information("Server GhostHero created: owner={Owner} level={Level}#{Sub}", ownerGuid, ghost.LevelId, ghost.SubLevelIndex);

            return ghost;
        }
    }
}
