namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>
/// 本地 Hero 状态管理器
/// 追踪本地英雄的状态
/// </summary>
internal sealed class LocalHeroState : DisposableEventReceiver,
    IOnLocalHeroInitDone,
    IOnLocalHeroPostUpdate
{
    private readonly GuestClientSession session;

#if DEBUG


    /// <summary>
    /// 用于调试关卡改变时打印日志
    /// </summary>
    /// <remarks>
    /// 存储格式为：<c>string lastLevelSignature = $"{LevelId}#{SubLevelIndex}";</c>
    /// </remarks>
    private string debugLastLevelSignature = string.Empty;
#endif

    /// <summary>
    /// 外部获取hero上下文
    /// </summary>
    public LocalHeroContext Context { get; } = new();

    /// <summary>
    /// 同步时间
    /// </summary>
    public double SyncedTimeMs => this.session.SyncedTimeMs;

    public LocalHeroState(GuestClientSession session)
    {
        this.session = session;

        this.Context.HasHero = true;
        this.Context.IsAlive = !GuestClientSession.Game.hero.destroyed;

        this.SyncLevelSnapshot(GuestClientSession.Game);
    }

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        var game = Game.Class.ME;
        if (hero != game?.hero)
        {
            return;
        }

        this.Context.HasHero = true;
        this.Context.IsAlive = !hero.destroyed;

        this.SyncLevelSnapshot(game);
    }

    void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
    {
        var game = Game.Class.ME;
        if (hero != game?.hero)
        {
            return;
        }

        this.Context.HasHero = !hero.destroyed;
        this.Context.IsAlive = this.Context.HasHero && hero.life > 0;
        this.Context.InCinematic = game.hasCinematic();

        this.UpdateLevel(game, hero);
        this.UpdateLoaclTeam(game);
        UpdateArgetable(game);
    }

    private void UpdateLevel(Game game, Hero hero)
    {
        if (game == null)
        {
            this.ResetLevelInfo();
            return;
        }

        // hero存在时才进行状态刷新
        var level = hero?._level;
        if (level == null)
        {
            return;
        }

        this.Context.CurrentLevelId = level.map?.id?.ToString() ?? string.Empty;
        this.Context.SubLevelIndex = SafeGetSubLevelIndex(level);
        this.Context.InSubLevel = IsInSubLevel(game, level);

#if DEBUG
        var signature = $"{this.Context.CurrentLevelId}#{this.Context.SubLevelIndex}";
        if (signature != this.debugLastLevelSignature)
        {
            this.debugLastLevelSignature = signature;
            this.Logger.Information(
                "Local level: {LevelId}#{SubLevelIndex} inSubLevel={InSubLevel}",
                this.Context.CurrentLevelId, this.Context.SubLevelIndex, this.Context.InSubLevel);
        }
#endif
    }

    private void UpdateLoaclTeam(Game game)
    {
        var level = game.curLevel;
        var teamHero = level.teamHero;
        var hero = game.hero;

        if (teamHero == null)
        {
            return;
        }

        if (hero._team != teamHero)
        {
            hero._team = teamHero;
        }

        var found = false;
        for (var i = 0; i < teamHero.members.length; i++)
        {
            if (ReferenceEquals(teamHero.members.getDyn(i), hero))
            { found = true; break; }
        }
        if (!found)
        {
            teamHero.addMember(hero);
        }
    }

    public static void UpdateArgetable(Game game)
    {
        var hero = game.hero;
        if (!hero._targetable
            && hero.life > 0
            && !hero.destroyed
            && game != null
            && !game.hasCinematic()
            && !game.paused)
        {
            hero.set_targetable(true);
        }
    }

    /// <summary>
    /// 用于hero初始化时更新关卡信息
    /// </summary>
    /// <param name="game"></param>
    private void SyncLevelSnapshot(Game game)
    {
        if (game == null)
        {
            return;
        }

        var level = game.hero?._level ?? game.curLevel;
        if (level == null)
        {
            return;
        }

        this.Context.CurrentLevelId = level.map?.id?.ToString() ?? string.Empty;
        this.Context.SubLevelIndex = SafeGetSubLevelIndex(level);
        this.Context.InSubLevel = IsInSubLevel(game, level);
    }

    /// <summary>
    /// 子关卡id判断
    /// </summary>
    /// <param name="game"></param>
    /// <param name="curLevel"></param>
    /// <returns></returns>
    private static bool IsInSubLevel(Game game, Level curLevel)
    {
        var subLevels = game.subLevels;
        if (subLevels == null || subLevels.length == 0)
        {
            return false;
        }

        var mainLevel = subLevels.getDyn(0);
        return mainLevel != null && curLevel != mainLevel;
    }


    private static int SafeGetSubLevelIndex(Level level)
    {
        try
        { return level.GetSubLevelIndex(); }
        catch { return -1; }
    }

    /// <summary>
    /// 重置关卡信息
    /// </summary>
    private void ResetLevelInfo()
    {
        this.Context.CurrentLevelId = string.Empty;
        this.Context.SubLevelIndex = -1;
        this.Context.InSubLevel = false;
    }

    //查询
    public bool HasHero() => this.Context.HasHero;
    public bool IsAlive() => this.Context.IsAlive;
    public bool IsInSubLevel() => this.Context.InSubLevel;
    public bool IsInCinematic() => this.Context.InCinematic;

    protected override void MyDispose() => base.MyDispose();
}
