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
        /// 服务端 Ghost 使用的高生命
        /// </summary>
        private const int ServerLife = 1_000_000;

        /// <summary>
        /// 客户端id
        /// </summary>
        public string OwnerGuid { get; }

        /// <summary>
        /// 是否由服务端创建
        /// </summary>
        public bool IsServer { get; }

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

        /// <summary>
        /// Ghost 底层实体是否仍可用
        /// </summary>
        public bool IsAlive => Hero != null && !Hero.destroyed;

        private GhostHero(Beheaded hero, string ownerGuid, bool isServer)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            IsServer = isServer;
            Actions = new GhostActions(hero, ownerGuid);
        }

        /// <summary>
        /// 服务端 Ghost 不参与真实扣血
        /// </summary>
        public void SetServerVitals()
        {
            if (!IsServer)
                return;

            Hero.maxLife = ServerLife;
            Hero.life = ServerLife;
        }

        /// <summary>
        /// 显式保证 Ghost 在 teamHero.members 中
        /// </summary>
        public void EnsureTeamMember()
        {
            if (!IsServer || Hero._team == null)
                return;

            var members = Hero._team.members;
            if (members == null)
                return;

            for (int i = 0; i < members.length; i++)
            {
                if (ReferenceEquals(
                        AffectCodec.ToObject<Entity>(members.getDyn(i)), Hero))
                    return;
            }

            Hero._team.addMember(Hero);
        }

        /// <summary>
        /// 服务端 Ghost 只作为运行时实体，不写入服务端 save
        /// </summary>
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
            if (!IsServer)
                return;

            if (enabled)
            {
                Hero.ignoreDetection = false;
                Hero.visible = true;
                Hero.spr.visible = true;
            }

            if (Hero._targetable != enabled)
            {
                Hero.set_targetable(enabled);
            }

            if (enabled)
                EnsureTeamMember();
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
            if (level == null || !IsAlive) return false;
            if (!ReferenceEquals(Hero._level, level)) return false;
            return SubLevelIndex == level.GetSubLevelIndex();
        }

        /// <summary>
        /// 把服务端 Ghost 迁移到新关卡
        /// </summary>
        public void Relocate(Level level, int cx, int cy)
        {
            if (!IsServer || level == null || Hero.destroyed)
                return;

            if (!ReferenceEquals(Hero._level, level))
            {
                var oldLevel = Hero._level;

                Hero.set_level(level);
                Hero.set_team(level.teamHero);
                Hero.initGfx();
                Hero.lastRoomId = -1;
                ExcludeFromSave();
                Hero.setPosCase(cx, cy, null, null);
                Hero.cancelVelocities();
                Hero.cancelMainSkills(null);
                Hero.minimapTracking();
                Hero.sprAlpha = 1.0;
                Hero.onLevelChanged(oldLevel);
            }

            EnsureOpposing(level.teamHero, level.teamMob);
            EnsureOpposing(level.teamHero, level.teamProp);
            EnsureOpposing(level.teamMob, level.teamHero);
            EnsureOpposing(level.teamProp, level.teamHero);

            Hero.visible = true;
            Hero.spr.visible = true;
            SetServerVitals();
            SyncLevel();
        }

        /// <summary>
        /// 位移至目标点,用于精准释放技能
        /// </summary>
        public void SetTransform(int cx, int cy, double xr, double yr, int dir, double dx, double dy)
        {
            Hero.setPosCase(cx, cy, xr, yr);
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

            if (IsServer)
            {

                if (Hero._targetable && Hero._team != null)
                    Hero.set_targetable(false);

                if (Hero._level != null)
                {
                    if (!Hero.destroyed)
                        Hero.destroy();

                    Hero.dispose();
                    Hero.set_level(null);
                }
            }
            else
            {
                Hero._team = null;
                Hero._level = null;
            }

            Logger.Information("GhostHero disposed: owner={Owner}", OwnerGuid);
        }

        /// <summary>
        /// 创建一个未注册的隐形Hero
        /// </summary>
        public static GhostHero? Create(
            Game? game,
            Level? level,
            string ownerGuid,
            bool isserver = false,
            int cx = 0,
            int cy = 0)
        {
            if (game == null || level == null) return null;

            var reference = game.hero;

            var hero = new Beheaded(game);

            if (isserver)
            {
                hero._targetable = false;
                hero.wakeup(level, cx, cy);

                hero.life = ServerLife;
                hero.maxLife = ServerLife;
                hero.visible = true;
                hero.spr.visible = true;
            }
            else
            {
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

                hero.visible = false;
                hero.spr.visible = false;
            }

            var ghost = new GhostHero(hero, ownerGuid, isserver);
            ghost.SyncLevel();

            if (isserver)
            {
                ghost.ExcludeFromSave();
            }

            return ghost;
        }
    }
}
