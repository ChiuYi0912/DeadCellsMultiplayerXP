namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
using dc.tool.atk;

/// <summary>
/// 战斗结算 Hook 的端侧接收者。
/// </summary>
internal interface IServerCombatHookSink
{
    public void OnApplyHitResult(
        Hook__AttackUtils.orig_applyHitResult orig,
        AttackData atk,
        Entity target);
}
