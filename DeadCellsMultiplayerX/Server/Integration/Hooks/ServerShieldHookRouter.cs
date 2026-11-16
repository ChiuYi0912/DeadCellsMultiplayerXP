namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.en;
using dc.tool.atk;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;

/// <summary>
/// 盾牌 Hook 路由。
/// </summary>
internal sealed class ServerShieldHookRouter(ServerMain server) : IServerShieldHookSink
{
    private readonly ServerMain server = server;

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

        var session = this.server.Session;
        if (session?.Ghosts.TryGetCombatant(self.owner, out var hero) == true)
        {
            session.Ghosts.Shield.OnBlock(
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

        var session = this.server.Session;
        if (session?.Ghosts.TryGetCombatant(self.owner, out _) == true)
        {
            session.Ghosts.Shield.OnCounter(
                self, sourceAtk, fullParry);
        }
    }
}
