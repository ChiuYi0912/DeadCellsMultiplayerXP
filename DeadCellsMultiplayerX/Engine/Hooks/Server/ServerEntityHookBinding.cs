namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 实体 Hook 的唯一订阅入口。
/// </summary>
internal static class ServerEntityHookBinding
{
    public static IDisposable Install(IServerEntityHookSink sink)
    {
        Hook_Entity.setColorMap += sink.OnSetColorMap;
        Hook_Entity._isOnScreen += sink.OnIsOnScreen;
        Hook_Entity.canBeDetected += sink.OnCanBeDetected;
        Hook_Entity.dispose += sink.OnDispose;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_Entity.setColorMap -= sink.OnSetColorMap));
        group.Add(new HookLease(() => Hook_Entity._isOnScreen -= sink.OnIsOnScreen));
        group.Add(new HookLease(() => Hook_Entity.canBeDetected -= sink.OnCanBeDetected));
        group.Add(new HookLease(() => Hook_Entity.dispose -= sink.OnDispose));
        return group;
    }
}
