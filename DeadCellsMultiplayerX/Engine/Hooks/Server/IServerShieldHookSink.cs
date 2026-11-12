namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.en;
using dc.tool.weap;

/// <summary>
/// 盾牌 Hook 的端侧接收者。
/// </summary>
internal interface IServerShieldHookSink
{
    public void OnCounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source);
}
