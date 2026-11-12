namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using DeadCellsMultiplayerX.Server.Core;

/// <summary>
/// 服务端 Hook 组合根。
/// 每个 Hook 只由一个 Binding 安装一次，再路由到具体 Router。
/// </summary>
internal sealed class ServerHookBootstrap(ServerMain server) : IDisposable
{
    private readonly ServerMain server = server;
    private readonly HookGroup group = new();

    public void Install()
    {
        this.group.Add(ServerLifecycleHookBinding.Install(new ServerLifecycleHookRouter(this.server)));
        this.group.Add(ServerEntityHookBinding.Install(new ServerEntityHookRouter(this.server)));
        this.group.Add(ServerMobHookBinding.Install(new ServerMobHookRouter(this.server)));
        this.group.Add(ServerHeroHookBinding.Install(new ServerHeroHookRouter(this.server)));
        this.group.Add(ServerCombatHookBinding.Install(new ServerCombatHookRouter()));
        this.group.Add(ServerShieldHookBinding.Install(new ServerShieldHookRouter()));
        this.group.Add(ServerPowerHookBinding.Install(new ServerPowerHookRouter()));
        this.group.Add(ServerLevelHookBinding.Install(new ServerLevelHookRouter(this.server)));
    }

    /// <inheritdoc />
    public void Dispose() => this.group.Dispose();
}
