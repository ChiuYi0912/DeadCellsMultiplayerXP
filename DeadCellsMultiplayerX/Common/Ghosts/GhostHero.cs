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
    internal sealed class GhostHero
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "GhostHero");

        public Beheaded Hero { get; }

        public string OwnerGuid { get; }

        public string LevelId { get; private set; } = string.Empty;

        public int SubLevelIndex { get; private set; } = -1;

        public GhostActions Actions { get; }

        private GhostHero(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new GhostActions(hero, ownerGuid);
        }

        /// <summary>
        /// 创建一个未注册的隐形Hero
        /// </summary>
        public static GhostHero? Create(Game? game, Level? level, string ownerGuid, bool isserver = false)
        {
            if (game == null || level == null) return null;

            var hero = new Beheaded(game);
            hero.init();

            // 使用set_level/set_team会注册实体 存档与队伍成员
            hero._level = level;
            hero._targetable = false;
            hero._team = level.teamHero;
            hero.life = 1000;
            hero.maxLife = 1000;

            EnsureTeamLinks(level);

            var ghost = new GhostHero(hero, ownerGuid);
            ghost.SyncLevel();

            var reference = game.hero;
            hero.initSprite(reference.spr.lib, reference.spr.groupName, null, null, null, false, null, null);

            hero.visible = isserver;
            hero.spr.visible = isserver;

            Logger.Information("GhostHero created: owner={Owner} level={Level}#{Sub}", ownerGuid, ghost.LevelId, ghost.SubLevelIndex);

            return ghost;
        }


        /// <summary>
        /// 服务端/客户端加载的关卡可能缺少 teamHero ↔ teamMob 的 opposingTeams 链接
        /// 补上后具体武器的 TeamIterator / isOpponent / Bullet 才能命中
        /// </summary>
        private static void EnsureTeamLinks(Level level)
        {
            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);
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
                if (ReferenceEquals(
                        AffectCodec.ToObject<Team>(opposing.getDyn(i)), b))
                    return;
            }

            opposing.push(b);
            Logger.Information(
                "Team link repaired: {A} -> {B}", a.__uid, b.__uid);
        }

        public void SyncLevel()
        {
            LevelId = Hero._level?.map?.id?.ToString() ?? string.Empty;
            SubLevelIndex = Hero._level?.GetSubLevelIndex() ?? -1;
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

        public void SetTransform(
            int cx, int cy, double xr, double yr, int dir, double dx, double dy)
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

        /// <summary>
        /// 预留给后续武器技能状态tick
        /// </summary>
        public void Tick(double dt)
        {
            Actions.Tick(dt);
        }

        public void Dispose()
        {
            Actions.Dispose();

            Hero._team = null;
            Hero._level = null;

            Logger.Information("GhostHero disposed: owner={Owner}", OwnerGuid);
        }
    }
}
