using dc.en;
using dc.libs.heaps.slib;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;

using dc;
namespace DeadCellsMultiplayerX.Server.Integration.Hooks
{
    /// <summary>
    /// 实体级 Hook 路由：颜色、可见性、销毁与注册表清理。
    /// </summary>
    internal sealed class ServerEntityHookRouter : IServerEntityHookSink
    {
        private readonly ServerMain server;

        public ServerEntityHookRouter(ServerMain server)
        {
            this.server = server;
        }

        public void OnSetColorMap(
            Hook_Entity.orig_setColorMap orig,
            Entity self,
            dc.String model,
            dc.String skin,
            HSprite sspr)
        {
            orig(self, model, skin, sspr);
        }

        /// <summary>
        /// 服务端不做视口裁剪。
        /// </summary>
        public bool OnIsOnScreen(Hook_Entity.orig__isOnScreen orig, Entity self)
        {
            return true;
        }

        /// <summary>
        /// 服务端战斗代理不能被隐身或 ignoreDetection 清掉目标。
        /// </summary>
        public bool OnCanBeDetected(Hook_Entity.orig_canBeDetected orig, Entity self)
        {
            if (server.Session?.Ghosts.TryGetCombatant(self, out _) == true)
                return !self.destroyed;

            return orig(self);
        }

        public void OnDispose(Hook_Entity.orig_dispose orig, Entity self)
        {
            var session = server.Session;

            if (self is Mob mob)
            {
                session?.Ghosts.Mobs.Detach(mob);
                session?.Entities.Unregister(mob);
            }

            orig(self);
        }
    }
}
