namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc.hl.types;
using dc.level;
using dc.pr;
using dc.ui;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;

/// <summary>
/// 调试 HUD Hook 的端侧接收者。
/// </summary>
internal interface IClientDebugHookSink
{
    public void OnPostUpdate(Hook_DebugHud.orig_postUpdate orig, DebugHud self);

    public void OnConstructor(Hook__DebugHud.orig___constructor__ orig, DebugHud self);

    public void OnInitLogsUi(Hook_DebugHud.orig_initLogsUi orig, DebugHud self);

    public void OnGameDispose(Hook_Game.orig_onDispose orig, Game self);

    public void OnLevelDispConstructor(
        Hook__LevelDisp.orig___constructor__ orig,
        LevelDisp arg1,
        Level p,
        LevelMap map,
        ArrayObj parallaxInfo);

    public void OnConsoleConstructor(HashlinkClosure orig, HashlinkObject self);
}
