using System;
using dc;
using dc.en.hero;
using dc.hl.types;
using dc.pr;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Common.Serializers;
using DeadCellsMultiplayerX.Utils;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Server.World.Ghosts
{
    /// <summary>
    /// 服务端 Ghost 代表远端玩家的权威战斗实体
    /// 不参与真实扣血，只负责队伍、目标与动作重放
    /// </summary>
    internal sealed class ServerGhostHero : IGhostEntity
    {
        private const int ServerLife = 1_000_000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerGhostHero");

        private bool disposed;

        private ServerGhostHero(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new ServerGhostActions(hero, ownerGuid);
            LevelKey = GhostLevelKey.From(hero._level);
        }

        public Beheaded Hero { get; }

        public string OwnerGuid { get; }

        public ServerGhostActions Actions { get; }

        public bool IsAlive => Hero != null && !Hero.destroyed;

        public GhostLevelKey LevelKey { get; private set; }

        public void SetServerVitals()
        {
            Hero.maxLife = ServerLife;
            Hero.life = ServerLife;
        }

        /// <summary>
        /// 显式保证 Ghost 在 teamHero.members 中。
        /// </summary>
        public void EnsureTeamMember()
        {
            if (Hero._team == null) return;

            var members = Hero._team.members;
            if (members == null) return;

            for (int i = 0; i < members.length; i++)
            {
                if (ReferenceEquals(
                        AffectCodec.ToObject<Entity>(members.getDyn(i)), Hero))
                    return;
            }

            Hero._team.addMember(Hero);
        }

        public void ExcludeFromSave()
        {
            var level = Hero._level;
            level?.savedEntities?.remove(Hero);
        }

        /// <summary>
        /// 控制服务端 Ghost 是否加入 teamHero 并成为 mob 的目标
        /// </summary>
        public void SetCombatEnabled(bool enabled)
        {
            if (disposed || Hero.destroyed) return;

            if (enabled)
            {
                Hero.ignoreDetection = false;
                Hero.visible = true;
                Hero.spr.visible = true;
            }

            if (Hero._targetable != enabled)
                Hero.set_targetable(enabled);

            if (enabled)
                EnsureTeamMember();
        }

        public void SyncLevel()
        {
            LevelKey = GhostLevelKey.From(Hero._level);
        }

        /// <summary>
        /// 只有同一个 Level 实例且 levelId/subLevel 都一致才复用
        /// 否则由 Actor 销毁重建
        /// </summary>
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
                Hero._team = null;
            }

            if (Hero._level != null && !Hero.destroyed)
            {
                Hero.destroy();
                Hero.dispose();
                Hero.set_level(null);
            }

            Logger.Information("ServerGhostHero disposed: owner={Owner}", OwnerGuid);
        }

        private static void EnsureOpposing(dc.tool.Team? a, dc.tool.Team? b)
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
                        AffectCodec.ToObject<dc.tool.Team>(opposing.getDyn(i)), b))
                    return;
            }

            opposing.push(b);
        }

        public static ServerGhostHero? CreateServer(
            Game? game,
            Level? level,
            string ownerGuid,
            int cx = 0,
            int cy = 0)
        {
            if (game == null || level == null)
                return null;

            var hero = new Beheaded(game);
            hero._targetable = false;
            hero.wakeup(level, cx, cy);

            hero.setHeadDepth(dc.Const.Class.DP_ROOM_MAIN_HERO);

            hero.life = ServerLife;
            hero.maxLife = ServerLife;
            hero.visible = true;
            hero.spr.visible = true;

            var ghost = new ServerGhostHero(hero, ownerGuid);
            ghost.SyncLevel();
            ghost.ExcludeFromSave();

            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);

            return ghost;
        }
    }
}
