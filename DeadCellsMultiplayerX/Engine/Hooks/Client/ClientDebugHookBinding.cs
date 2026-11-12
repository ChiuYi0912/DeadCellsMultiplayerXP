namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc.level;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using ModCore.Modules;

/// <summary>
/// 调试 HUD Hook 的唯一订阅入口。
/// </summary>
internal static class ClientDebugHookBinding
{
    public static IDisposable Install(IClientDebugHookSink sink)
    {
        Hook_DebugHud.postUpdate += sink.OnPostUpdate;
        Hook__DebugHud.__constructor__ += sink.OnConstructor;
        Hook_DebugHud.initLogsUi += sink.OnInitLogsUi;
        Hook_Game.onDispose += sink.OnGameDispose;
        Hook__LevelDisp.__constructor__ += sink.OnLevelDispConstructor;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_DebugHud.postUpdate -= sink.OnPostUpdate));
        group.Add(new HookLease(() => Hook__DebugHud.__constructor__ -= sink.OnConstructor));
        group.Add(new HookLease(() => Hook_DebugHud.initLogsUi -= sink.OnInitLogsUi));
        group.Add(new HookLease(() => Hook_Game.onDispose -= sink.OnGameDispose));
        group.Add(new HookLease(() => Hook__LevelDisp.__constructor__ -= sink.OnLevelDispConstructor));

        var consoleHook = HashlinkHooks.Instance.CreateHook(
            "ui.$Console", "__constructor__", sink.OnConsoleConstructor);
        consoleHook.Enable();
        group.Add(new RawHookLease(consoleHook));
        return group;
    }
}
