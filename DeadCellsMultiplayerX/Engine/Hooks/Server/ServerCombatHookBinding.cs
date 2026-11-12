using dc.tool.atk;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 战斗结算 Hook 的唯一订阅入口。
    /// </summary>
    internal static class ServerCombatHookBinding
    {
        public static IDisposable Install(IServerCombatHookSink sink)
        {
            Hook__AttackUtils.applyHitResult += sink.OnApplyHitResult;

            var group = new HookGroup();
            group.Add(new HookLease(() => Hook__AttackUtils.applyHitResult -= sink.OnApplyHitResult));
            return group;
        }
    }
}
