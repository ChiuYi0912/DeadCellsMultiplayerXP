namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay;

using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;

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
        this.Hero = hero;
        this.OwnerGuid = ownerGuid;
        this.Actions = new ClientHeroActionRuntime(hero, ownerGuid);
        this.LevelKey = LevelUtils.KeyOf(hero._level);
    }

    public Beheaded Hero { get; }

    public string OwnerGuid { get; }

    public ClientHeroActionRuntime Actions { get; }

    public bool IsAlive => this.Hero != null && !this.Hero.destroyed;

    public LevelKey LevelKey { get; private set; }

    /// <summary>
    /// Mob 技能需要目标存活时使用的占位生命，避免被本地伤害打死。
    /// </summary>
    public void SetPlaceholderVitals(int life = 9999)
    {
        if (this.disposed || this.Hero.destroyed)
        {
            return;
        }

        this.Hero.maxLife = life;
        this.Hero.life = life;
    }

    public bool MatchesLevel(Level? level)
    {
        if (level == null || !this.IsAlive)
        {
            return false;
        }

        if (!ReferenceEquals(this.Hero._level, level))
        {
            return false;
        }

        return LevelUtils.Matches(level, this.LevelKey);
    }

    public void SetTransform(
        int cx, int cy, double xr, double yr, int dir, double dx, double dy)
    {
        this.Hero.setPosCase(cx, cy, xr, yr);
        this.Hero.dir = dir;
        this.Hero.dx = dx;
        this.Hero.dy = dy;
    }

    public void SetTiers(int brutality, int survival, int tactic)
    {
        this.Hero.setBrutalityTier(brutality);
        this.Hero.setSurvivalTier(survival);
        this.Hero.setTacticTier(tactic);
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        this.Actions.Dispose();
        HeroProxyFactory.DisposeClient(this.Hero);
        Logger.Information("ClientHeroReplayProxy disposed: owner={Owner}", this.OwnerGuid);
    }

    public static ClientHeroReplayProxy? CreateClient(
        Game? game,
        Level? level,
        string ownerGuid)
    {
        var hero = HeroProxyFactory.CreateClient(
            game, level, game?.hero);
        if (hero == null)
        {
            return null;
        }

        return new ClientHeroReplayProxy(hero, ownerGuid);
    }
}
