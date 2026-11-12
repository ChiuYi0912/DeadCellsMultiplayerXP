using dc.tool.atk;

using dc;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 战斗结算 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerCombatHookSink
    {
        void OnApplyHitResult(
            Hook__AttackUtils.orig_applyHitResult orig,
            AttackData atk,
            Entity target);
    }
}
