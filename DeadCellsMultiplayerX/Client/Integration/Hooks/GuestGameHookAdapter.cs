namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using dc.cine;
using dc.en;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using HaxeProxy.Runtime;
using ModCore.Events;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// 会话游戏生命周期与关卡切换 Hook：Game 初始化/退出、LevelTransition 投票。
/// 会话与退出动作通过委托注入，适配器不持有 <c>GuestClientSession</c>。
/// </summary>
internal sealed class GuestGameHookAdapter(
    ILogger logger,
    Func<GuestClient?> currentGuest,
    Action quit,
    Action dispose)
{
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    private readonly Func<GuestClient?> currentGuest = currentGuest
            ?? throw new ArgumentNullException(nameof(currentGuest));
    private readonly Action quit = quit
            ?? throw new ArgumentNullException(nameof(quit));
    private readonly Action dispose = dispose
            ?? throw new ArgumentNullException(nameof(dispose));

    public void GameInit(Hook_Game.orig_init orig, Game self)
    {
        orig(self);
        this.logger.Information("Guest session Game.init");
        self.infection.destroy();
        self.gameSignals.heroInitDone.add(
            new HlAction<Hero>((hero) =>
                EventSystem.BroadcastEvent<IOnLocalHeroInitDone, Hero>(hero)),
            null, null, null);
#if DEBUG
        dc.ui.Console.Class.ME.flags.set("area".AsHaxeString(), 1);
        DebugFlags.Set("netstat", true);
        DebugFlags.Set("entityLabels", true);
        DebugHUD.EnsureNative();
#endif
    }

    public void GameDispose(Hook_Game.orig_onDispose orig, Game self)
    {
        orig(self);

        this.logger.Information("Quiting...");

        this.quit();
        this.dispose();
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

        GuestClient? current = this.currentGuest();

        if (current == null)
        {
            orig(arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        Hero hero = Game.Class.ME.hero;

        if (current.IsServerDrivenTransition)
        {
            if (hero != null && !hero.destroyed)
            {
                hero.set_targetable(true);
                hero.unlockControls();
            }

            orig(arg1, mainId, map, linkId, heroPosAfterBossRuneReload, noLoadingData);
            return;
        }

        _ = current.SelectNextLevel(levelid);

        if (hero != null && !hero.destroyed)
        {
            hero.set_targetable(false);
            hero.lockControlsF(999999);
        }
    }
}
