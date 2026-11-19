namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc;
using dc.en;
using dc.libs.heaps.slib;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server;

/// <summary>
/// 实体级 Hook 路由：颜色、可见性、销毁与注册表清理。
/// </summary>
internal sealed class ServerEntityHookRouter(ServerRuntime server) : IServerEntityHookSink
{
    private readonly ServerRuntime server = server;

    public void OnSetColorMap(
        Hook_Entity.orig_setColorMap orig,
        Entity self,
        String model,
        String skin,
        HSprite sspr) => orig(self, model, skin, sspr);

    /// <summary>
    /// 服务端不做视口裁剪。
    /// </summary>
    public bool OnIsOnScreen(Hook_Entity.orig__isOnScreen orig, Entity self) => true;

    /// <summary>
    /// 服务端战斗代理不能被隐身或 ignoreDetection 清掉目标。
    /// </summary>
    public bool OnCanBeDetected(Hook_Entity.orig_canBeDetected orig, Entity self)
    {
        if (this.server.Worlds?.CurrentWorld?.Actors.TryGetCombatant(self, out _) == true)
        {
            return !self.destroyed;
        }

        return orig(self);
    }

    public void OnDispose(Hook_Entity.orig_dispose orig, Entity self)
    {
        var world = this.server.Worlds?.CurrentWorld;

        if (self is Mob mob)
        {
            world?.Actors.Mobs.Detach(mob);
            world?.Entities.Unregister(mob);
        }

        orig(self);
    }
}
