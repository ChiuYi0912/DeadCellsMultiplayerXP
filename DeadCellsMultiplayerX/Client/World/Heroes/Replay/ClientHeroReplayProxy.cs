using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using Serilog;
using DeadCellsMultiplayerX.Engine.Utilities;
namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay
{
    /// <summary>
    /// 客户端 Ghost：用于技能/武器重放的隐形 Hero。
    /// 只存在于本地关卡，不参与存档与权威结算。
    /// </summary>
    internal sealed class ClientHeroReplayProxy : IGhostEntity
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ClientHeroReplayProxy");

        private bool disposed;

        private ClientHeroReplayProxy(Beheaded hero, string ownerGuid)
        {
            Hero = hero;
            OwnerGuid = ownerGuid;
            Actions = new ClientHeroActionRuntime(hero, ownerGuid);
            LevelKey = LevelUtils.KeyOf(hero._level);
        }

        public Beheaded Hero { get; }

        public string OwnerGuid { get; }

        public ClientHeroActionRuntime Actions { get; }

        public bool IsAlive => Hero != null && !Hero.destroyed;

        public LevelKey LevelKey { get; private set; }

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
            HeroProxyFactory.DisposeClient(Hero);
            Logger.Information("ClientHeroReplayProxy disposed: owner={Owner}", OwnerGuid);
        }

        public static ClientHeroReplayProxy? CreateClient(
            Game? game,
            Level? level,
            string ownerGuid)
        {
            var hero = HeroProxyFactory.CreateClient(
                game, level, game?.hero);
            if (hero == null)
                return null;

            return new ClientHeroReplayProxy(hero, ownerGuid);
        }
    }
}
