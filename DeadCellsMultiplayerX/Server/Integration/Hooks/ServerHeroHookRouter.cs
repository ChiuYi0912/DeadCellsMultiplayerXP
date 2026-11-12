namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;

/// <summary>
/// Hero Hook 路由：服务端战斗代理的更新、伤害与颜色。
/// </summary>
internal sealed class ServerHeroHookRouter(ServerMain server) : IServerHeroHookSink
{
    private readonly ServerMain server = server;

    public void OnFixedUpdate(Hook_Hero.orig_fixedUpdate orig, Hero self)
    {
        var session = this.server.Session;
        if (session?.Ghosts.TryGetCombatant(self, out _) == true)
        {
            session.Ghosts.Aggro.RunAsHero(self, () => orig(self));
        }
        else
        {
            orig(self);
        }
    }

    public void OnPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self)
    {
        var session = this.server.Session;
        if (session?.Ghosts.TryGetCombatant(self, out _) == true)
        {
            session.Ghosts.Aggro.RunAsHero(self, () => orig(self));
        }
        else
        {
            orig(self);
        }
    }

    public void OnDamage(Hook_Hero.orig_onDamage orig, Hero self, AttackData atk)
    {
        var session = this.server.Session;
        if (session?.Ghosts.TryGetCombatant(self, out _) == true)
        {
            session.Ghosts.Aggro.RunAsHero(self, () => orig(self, atk));
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
        if (this.server.Session?.Ghosts.TryGetCombatant(self, out _) == true)
        {
            return !self.destroyed && self.life > 0;
        }

        return orig(self, by);
    }

    public void OnInitColorMap(Hook_Hero.orig_initColorMap orig, Hero self) => orig(self);
}
