using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Integration.Hooks
{
    /// <summary>
    /// 服务端 Hook 组合根。
    /// 每个 Hook 只由一个 Binding 安装一次，再路由到具体 Router。
    /// </summary>
    internal sealed class ServerHookBootstrap : IDisposable
    {
        private readonly ServerMain server;
        private readonly HookGroup group = new();

        public ServerHookBootstrap(ServerMain server)
        {
            this.server = server;
        }

        public void Install()
        {
            group.Add(ServerLifecycleHookBinding.Install(new ServerLifecycleHookRouter(server)));
            group.Add(ServerEntityHookBinding.Install(new ServerEntityHookRouter(server)));
            group.Add(ServerMobHookBinding.Install(new ServerMobHookRouter(server)));
            group.Add(ServerHeroHookBinding.Install(new ServerHeroHookRouter(server)));
            group.Add(ServerCombatHookBinding.Install(new ServerCombatHookRouter()));
            group.Add(ServerShieldHookBinding.Install(new ServerShieldHookRouter()));
            group.Add(ServerPowerHookBinding.Install(new ServerPowerHookRouter()));
            group.Add(ServerLevelHookBinding.Install(new ServerLevelHookRouter(server)));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            group.Dispose();
        }
    }
}
