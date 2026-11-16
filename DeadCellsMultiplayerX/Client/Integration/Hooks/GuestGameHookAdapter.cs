namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using dc.cine;
using dc.en;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using HaxeProxy.Runtime;
using ModCore.Events;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// 会话游戏生命周期与关卡切换 Hook：Game 初始化/退出、LevelTransition 投票。
/// 会话与退出动作通过委托注入，适配器不持有 <c>GuestGameSession</c>。
/// </summary>
internal sealed class GuestGameHookAdapter(
    ILogger logger,
    Func<bool> isStopping,
    Func<bool> isServerDrivenTransition,
    Func<string, Task> submitLevelVote,
    Action quit) : IDisposable
{
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    private readonly Action quit = quit
            ?? throw new ArgumentNullException(nameof(quit));
    private Game? ownedGame;
    private HlAction<Hero>? heroInitialized;

    public void Dispose()
    {
        var game = this.ownedGame;
        var callback = this.heroInitialized;
        this.ownedGame = null;
        this.heroInitialized = null;
        if (game != null && callback != null)
        {
            game.gameSignals.heroInitDone.remove(callback);
        }
    }

    public void GameInit(Hook_Game.orig_init orig, Game self)
    {
        orig(self);
        if (isStopping())
        {
            return;
        }

        this.RemovePreviousSignal();
        this.ownedGame = self;
        this.logger.Information("Guest session Game.init");
        self.infection.destroy();
        this.heroInitialized = new HlAction<Hero>(hero =>
        {
            if (!isStopping() && this.ownedGame == self)
            {
                EventSystem.BroadcastEvent<IOnLocalHeroInitDone, Hero>(hero);
            }
        });
        self.gameSignals.heroInitDone.add(this.heroInitialized, null, null, null);
#if DEBUG
        dc.ui.Console.Class.ME.flags.set("area".AsHaxeString(), 1);
        DebugFlags.Set("netstat", true);
        DebugFlags.Set("entityLabels", true);
        DebugHUD.EnsureNative();
#endif
    }

    public void GameDispose(Hook_Game.orig_onDispose orig, Game self)
    {
        var ownsGame = this.ownedGame == self;
        if (ownsGame)
        {
            this.RemovePreviousSignal();
        }
        try
        { orig(self); }
        finally
        {
            // cleanUser before the initial launch disposes an unowned game.
            // Disposing our own game, even while it is loading, is an exit.
            if (ownsGame && !isStopping())
            {
                this.quit();
            }
        }
    }

    private void RemovePreviousSignal()
    {
        try
        { this.Dispose(); }
        catch (Exception ex) { this.logger.Warning(ex, "Guest hero signal unsubscribe failed"); }
    }

    public void LevelTransitionCtor(
        Hook__LevelTransition.orig___constructor__ orig,
        LevelTransition arg1,
        dc.String mainId,
        dc.level.LevelMap map,
        int? linkId,
        CPoint heroPosAfterBossRuneReload,
        Ref<bool> noLoadingData)
    {
        var levelid = mainId?.ToString() ?? string.Empty;
        if (levelid == "PrisonStart" || string.IsNullOrEmpty(levelid))
        {
            orig(arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        if (isStopping())
        {
            orig(arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        var hero = Game.Class.ME.hero;

        if (isServerDrivenTransition())
        {
            if (hero != null && !hero.destroyed)
            {
                hero.set_targetable(true);
                hero.unlockControls();
            }

            orig(arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        _ = this.SubmitVoteAsync(levelid);

        if (hero != null && !hero.destroyed)
        {
            hero.set_targetable(false);
            hero.lockControlsF(999999);
        }
    }

    private async Task SubmitVoteAsync(string levelId)
    {
        try
        { await submitLevelVote(levelId); }
        catch (Exception ex) when (isStopping()) { this.logger.Debug(ex, "Level vote canceled during exit"); }
        catch (Exception ex) { this.logger.Warning(ex, "Level vote failed"); }
    }
}
