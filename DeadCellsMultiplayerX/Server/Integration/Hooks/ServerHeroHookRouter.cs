namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server;

/// <summary>
/// Hero Hook 路由：服务端战斗代理的更新、伤害与颜色。
/// </summary>
internal sealed class ServerHeroHookRouter(ServerRuntime server) : IServerHeroHookSink
{
    private readonly ServerRuntime server = server;

    public void OnFixedUpdate(Hook_Hero.orig_fixedUpdate orig, Hero self)
    {
        var world = this.server.Worlds?.CurrentWorld;
        if (world?.Actors.TryGetCombatant(self, out _) == true)
        {
            world.Actors.Aggro.RunAsHero(self, () => orig(self));
        }
        else
        {
            orig(self);
        }
    }

    public void OnPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self)
    {
        var world = this.server.Worlds?.CurrentWorld;
        if (world?.Actors.TryGetCombatant(self, out _) == true)
        {
            world.Actors.Aggro.RunAsHero(self, () => orig(self));
        }
        else
        {
            orig(self);
        }
    }

    public void OnDamage(Hook_Hero.orig_onDamage orig, Hero self, AttackData atk)
    {
        var world = this.server.Worlds?.CurrentWorld;
        if (world?.Actors.TryGetCombatant(self, out _) == true)
        {
            world.Actors.Aggro.RunAsHero(self, () => orig(self, atk));
        }
        else
        {
            orig(self, atk);
        }
    }

    /// <summary>
    /// 服务端 Ghost 不参与暂停/电影状态判断，否则原版 updateThreat 会反复清目标。
    /// </summary>
    public bool OnCanBeHitBy(Hook_Hero.orig_canBeHitBy orig, Hero self, Entity by)
    {
        if (this.server.Worlds?.CurrentWorld?.Actors.TryGetCombatant(self, out _) == true)
        {
            return !self.destroyed && self.life > 0;
        }

        return orig(self, by);
    }

    public void OnInitColorMap(Hook_Hero.orig_initColorMap orig, Hero self) => orig(self);
}
