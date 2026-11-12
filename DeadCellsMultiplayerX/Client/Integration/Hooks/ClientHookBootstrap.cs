namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 客户端进程级 Hook 的组合根。
/// 每个 Hook 只由一个 Binding 安装一次。
/// </summary>
internal sealed class ClientHookBootstrap : IDisposable
{
    private readonly HookGroup group = new();

    public ClientHookBootstrap(IClientLifecycleHookSink sink) => this.group.Add(ClientLifecycleHookBinding.Install(sink));

    /// <inheritdoc />
    public void Dispose() => this.group.Dispose();
}
