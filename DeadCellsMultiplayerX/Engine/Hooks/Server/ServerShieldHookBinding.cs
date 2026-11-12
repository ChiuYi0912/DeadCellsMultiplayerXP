using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 盾牌 Hook 的唯一订阅入口。
    /// </summary>
    internal static class ServerShieldHookBinding
    {
        public static IDisposable Install(IServerShieldHookSink sink)
        {
            Hook_BaseShield.counterGrenade += sink.OnCounterGrenade;

            var group = new HookGroup();
            group.Add(new HookLease(() => Hook_BaseShield.counterGrenade -= sink.OnCounterGrenade));
            return group;
        }
    }
}
