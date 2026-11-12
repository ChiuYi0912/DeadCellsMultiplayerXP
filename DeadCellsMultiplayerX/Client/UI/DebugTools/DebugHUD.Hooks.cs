namespace DeadCellsMultiplayerX.Client.UI.DebugTools;

using dc.hl.types;
using dc.level;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;

/// <summary>
/// 调试 HUD Hook 的接口转发；处理逻辑保持在原方法中。
/// </summary>
public partial class DebugHUD
{
    void IClientDebugHookSink.OnPostUpdate(
        Hook_DebugHud.orig_postUpdate orig,
        DebugHud self)
        => this.Hook_DebugHud_postUpdate(orig, self);

    void IClientDebugHookSink.OnConstructor(
        Hook__DebugHud.orig___constructor__ orig,
        DebugHud self)
        => Hook_DebugHud_initialize(orig, self);

    void IClientDebugHookSink.OnInitLogsUi(
        Hook_DebugHud.orig_initLogsUi orig,
        DebugHud self)
        => this.Hook_DebugHud_initlogui(orig, self);

    void IClientDebugHookSink.OnGameDispose(
        Hook_Game.orig_onDispose orig,
        Game self)
        => this.HookGameOnDispose(orig, self);

    void IClientDebugHookSink.OnLevelDispConstructor(
        Hook__LevelDisp.orig___constructor__ orig,
        LevelDisp arg1,
        Level p,
        LevelMap map,
        ArrayObj parallaxInfo)
        => Hook_LevelDisp_init(orig, arg1, p, map, parallaxInfo);

    void IClientDebugHookSink.OnConsoleConstructor(
        HashlinkClosure orig,
        HashlinkObject self)
        => Hook_Console_ctor(orig, self);
}
