namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using DeadCellsMultiplayerX.Diagnostics;
using dc;
using dc.cine;
using dc.en;
using dc.level;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Engine.Adapters.Levels;
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
    Func<bool> isInitialLoading,
    Func<string?> authorizedLevelId,
    Func<string, Task> submitLevelVote,
    Func<LevelTransition, string, (long Generation, Game Game)?> tryBeginLevelTransition,
    Action<long, LevelTransition, Game, string> onLevelConstructed,
    Action quit) : IDisposable
{
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    private readonly Action quit = quit
            ?? throw new ArgumentNullException(nameof(quit));
    private Game? ownedGame;
    private HlAction<Hero>? heroInitialized;

    public void Dispose() => this.RemovePreviousSignal();

    private void RemovePreviousSignal()
    {
        var game = this.ownedGame;
        var callback = this.heroInitialized;
        this.ownedGame = null;
        this.heroInitialized = null;
        if (game != null && callback != null)
        {
            try
            {
                game.gameSignals.heroInitDone.remove(callback);
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "Guest hero signal unsubscribe failed");
            }
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

    public void LevelTransitionCtor(
        Hook__LevelTransition.orig___constructor__ orig,
        LevelTransition self,
        String mainId,
        LevelMap map,
        int? linkId,
        CPoint heroPosAfterBossRuneReload,
        Ref<bool> noLoadingData)
    {
        if (mainId is null || isStopping() || isInitialLoading())
        {
            // Saved-game entry and sub-levels keep their native path.
            orig(self, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        var levelId = mainId.ToString() ?? string.Empty;
        var authorized = authorizedLevelId();
        if (authorized == null)
        {
            _ = this.SubmitVoteAsync(levelId);
            return;
        }

        if (!string.Equals(levelId, authorized, StringComparison.Ordinal))
        {
            this.logger.Warning("Ignoring unauthorized level: {LevelId}; expected {Authorized}", levelId, authorized);
            return;
        }
        var context = tryBeginLevelTransition(self, levelId);
        if (context == null)
        {
            return;
        }

        orig(self, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
        onLevelConstructed(context.Value.Generation, self, context.Value.Game, levelId);
    }

    public static void LevelTransition_loadNewLevel(
        Hook_LevelTransition.orig_loadNewLevel _, LevelTransition self)
        => NativeLevelTransition.Load(self);

    private async Task SubmitVoteAsync(string levelId)
    {
        try
        {
            await submitLevelVote(levelId);
        }
        catch (Exception ex) when (isStopping())
        {
            this.logger.Debug(ex, "Level vote canceled during exit");
        }
        catch (Exception ex)
        {
            // A failed response does not prove that the server rejected the vote.
            this.logger.Warning(ex, "Level vote failed; still waiting for server selection: {LevelId}", levelId);
        }
    }
}
