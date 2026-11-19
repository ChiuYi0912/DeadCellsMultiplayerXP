namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.en;
using dc.tool.atk;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server;

/// <summary>
/// 盾牌 Hook 路由。
/// </summary>
internal sealed class ServerShieldHookRouter(ServerRuntime server) : IServerShieldHookSink
{
    private readonly ServerRuntime server = server;

    public void OnCounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source) => ShieldGrenadeCounter.Run(self, () => orig(self, source));

    public void OnShieldBlock(
        Hook_BaseShield.orig_onShieldBlock orig,
        BaseShield self,
        AttackData sourceAtk,
        bool fullParry)
    {
        orig(self, sourceAtk, fullParry);

        if (self.owner == null)
        {
            return;
        }

        var world = this.server.Worlds?.CurrentWorld;
        if (world?.Actors.TryGetCombatant(self.owner, out var hero) == true)
        {
            world.Actors.Shield.OnBlock(
                hero, self, sourceAtk, fullParry);
        }
    }

    public void OnShieldCounterAttack(
        Hook_BaseShield.orig_shieldCounterAttack orig,
        BaseShield self,
        AttackData sourceAtk,
        bool fullParry)
    {
        orig(self, sourceAtk, fullParry);

        if (self.owner == null)
        {
            return;
        }

        var world = this.server.Worlds?.CurrentWorld;
        if (world?.Actors.TryGetCombatant(self.owner, out _) == true)
        {
            world.Actors.Shield.OnCounter(
                self, sourceAtk, fullParry);
        }
    }
}
