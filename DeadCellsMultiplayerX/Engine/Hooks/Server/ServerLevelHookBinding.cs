namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 关卡区域 Hook 的唯一订阅入口。
/// </summary>
internal static class ServerLevelHookBinding
{
    public static IDisposable Install(IServerLevelHookSink sink)
    {
        Hook_LevelAreaAffect.init += sink.OnAreaAffectInit;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_LevelAreaAffect.init -= sink.OnAreaAffectInit));
        return group;
    }
}
