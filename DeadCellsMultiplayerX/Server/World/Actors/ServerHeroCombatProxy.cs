using dc;
using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using Serilog;

using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
namespace DeadCellsMultiplayerX.Server.World.Actors
{
    /// <summary>
    /// 服务端 Ghost 代表远端玩家的权威战斗实体
    /// 不参与真实扣血，只负责队伍、目标与动作重放
    /// </summary>
    internal sealed class ServerHeroCombatProxy : IGhostEntity
    {
        private const int SERVER_LIFE = 1_000_000;

        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerHeroCombatProxy");

        private bool disposed;

        private ServerHeroCombatProxy(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new ServerHeroActionRuntime(hero, ownerGuid);
            LevelKey = LevelUtils.KeyOf(hero._level);
        }

        public Beheaded Hero { get; }

        public string OwnerGuid { get; }

        public ServerHeroActionRuntime Actions { get; }

        public bool IsAlive => Hero != null && !Hero.destroyed;

        public LevelKey LevelKey { get; private set; }

        public void SetServerVitals()
        {
            Hero.maxLife = SERVER_LIFE;
            Hero.life = SERVER_LIFE;
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
            LevelKey = LevelUtils.KeyOf(Hero._level);
        }

        /// <summary>
        /// 只有同一个 Level 实例且 levelId/subLevel 都一致才复用
        /// 否则由 Actor 销毁重建
        /// </summary>
        public bool MatchesLevel(Level? level)
        {
            if (level == null || !IsAlive) return false;
            if (!ReferenceEquals(Hero._level, level)) return false;
            return LevelUtils.Matches(level, LevelKey);
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
            HeroProxyFactory.DisposeServer(Hero);
            Logger.Information("ServerHeroCombatProxy disposed: owner={Owner}", OwnerGuid);
        }

        public static ServerHeroCombatProxy? CreateServer(
            Game? game,
            Level? level,
            string ownerGuid,
            int cx = 0,
            int cy = 0)
        {
            var hero = HeroProxyFactory.CreateServer(
                game, level, cx, cy, SERVER_LIFE);
            if (hero == null)
                return null;

            var ghost = new ServerHeroCombatProxy(hero, ownerGuid);
            ghost.SyncLevel();
            ghost.ExcludeFromSave();
            return ghost;
        }
    }
}
