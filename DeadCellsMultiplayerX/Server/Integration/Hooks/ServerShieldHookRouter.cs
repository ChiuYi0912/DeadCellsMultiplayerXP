namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.en;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using DeadCellsMultiplayerX.Engine.Hooks.Server;

/// <summary>
/// 盾牌 Hook 路由。
/// </summary>
internal sealed class ServerShieldHookRouter : IServerShieldHookSink
{
    public void OnCounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source) => ShieldGrenadeCounter.Run(self, () => orig(self, source));
}
