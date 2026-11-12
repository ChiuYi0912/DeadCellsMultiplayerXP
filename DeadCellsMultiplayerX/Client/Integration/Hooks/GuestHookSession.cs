namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 客户端会话级 Hook 的生命周期拥有者。
/// 会话结束时释放全部订阅。
/// </summary>
internal sealed class GuestHookSession : IDisposable
{
    private readonly HookGroup group = new();

    public GuestHookSession(IClientSessionHookSink sink) => this.group.Add(ClientSessionHookBinding.Install(sink));

    /// <inheritdoc />
    public void Dispose() => this.group.Dispose();
}
