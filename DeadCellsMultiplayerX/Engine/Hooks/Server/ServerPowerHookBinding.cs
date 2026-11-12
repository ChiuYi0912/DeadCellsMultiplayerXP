using dc;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// Power Hook 的唯一订阅入口。
    /// </summary>
    internal static class ServerPowerHookBinding
    {
        public static IDisposable Install(IServerPowerHookSink sink)
        {
            Hook_Power.update += sink.OnUpdate;

            var group = new HookGroup();
            group.Add(new HookLease(() => Hook_Power.update -= sink.OnUpdate));
            return group;
        }
    }
}
