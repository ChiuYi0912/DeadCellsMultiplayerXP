namespace DeadCellsMultiplayerX.Server.World.Actors;

using dc;
using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using DeadCellsMultiplayerX.Engine.Adapters.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;
using Serilog;

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
        this.Hero = hero;
        this.OwnerGuid = ownerGuid;
        this.Actions = new ServerHeroActionRuntime(hero, ownerGuid);
        this.LevelKey = LevelUtils.KeyOf(hero._level);
    }

    public Beheaded Hero { get; }

    public string OwnerGuid { get; }

    public ServerHeroActionRuntime Actions { get; }

    public bool IsAlive => this.Hero != null && !this.Hero.destroyed;

    public LevelKey LevelKey { get; private set; }

    public void SetServerVitals()
    {
        this.Hero.maxLife = SERVER_LIFE;
        this.Hero.life = SERVER_LIFE;
    }

    /// <summary>
    /// 显式保证 Ghost 在 teamHero.members 中。
    /// </summary>
    public void EnsureTeamMember()
    {
        if (this.Hero._team == null)
        {
            return;
        }

        var members = this.Hero._team.members;
        if (members == null)
        {
            return;
        }

        for (var i = 0; i < members.length; i++)
        {
            if (ReferenceEquals(
                    AffectCodec.ToObject<Entity>(members.getDyn(i)), this.Hero))
            {
                return;
            }
        }

        this.Hero._team.addMember(this.Hero);
    }

    public void ExcludeFromSave()
    {
        var level = this.Hero._level;
        level?.savedEntities?.remove(this.Hero);
    }

    /// <summary>
    /// 控制服务端 Ghost 是否加入 teamHero 并成为 mob 的目标
    /// </summary>
    public void SetCombatEnabled(bool enabled)
    {
        if (this.disposed || this.Hero.destroyed)
        {
            return;
        }

        if (enabled)
        {
            this.Hero.ignoreDetection = false;
            this.Hero.visible = true;
            this.Hero.spr.visible = true;
        }

        if (this.Hero._targetable != enabled)
        {
            this.Hero.set_targetable(enabled);
        }

        if (enabled)
        {
            this.EnsureTeamMember();
        }
    }

    public void SyncLevel() => this.LevelKey = LevelUtils.KeyOf(this.Hero._level);

    /// <summary>
    /// 只有同一个 Level 实例且 levelId/subLevel 都一致才复用
    /// 否则由 Actor 销毁重建
    /// </summary>
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
        HeroProxyFactory.DisposeServer(this.Hero);
        Logger.Information("ServerHeroCombatProxy disposed: owner={Owner}", this.OwnerGuid);
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
        {
            return null;
        }

        var ghost = new ServerHeroCombatProxy(hero, ownerGuid);
        ghost.SyncLevel();
        ghost.ExcludeFromSave();
        return ghost;
    }
}
