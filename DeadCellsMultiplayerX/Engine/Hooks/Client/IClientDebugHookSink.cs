using dc.hl.types;
using dc.level;
using dc.pr;
using dc.ui;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;

namespace DeadCellsMultiplayerX.Engine.Hooks.Client
{
    /// <summary>
    /// 调试 HUD Hook 的端侧接收者。
    /// </summary>
    internal interface IClientDebugHookSink
    {
        void OnPostUpdate(Hook_DebugHud.orig_postUpdate orig, DebugHud self);

        void OnConstructor(Hook__DebugHud.orig___constructor__ orig, DebugHud self);

        void OnInitLogsUi(Hook_DebugHud.orig_initLogsUi orig, DebugHud self);

        void OnGameDispose(Hook_Game.orig_onDispose orig, dc.pr.Game self);

        void OnLevelDispConstructor(
            Hook__LevelDisp.orig___constructor__ orig,
            LevelDisp arg1,
            Level p,
            LevelMap map,
            ArrayObj parallaxInfo);

        void OnConsoleConstructor(HashlinkClosure orig, HashlinkObject self);
    }
}
