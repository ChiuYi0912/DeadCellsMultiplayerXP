namespace DeadCellsMultiplayerX.Engine.Hooks.Shared;

using ModCore.Modules;

/// <summary>
/// Hashlink 原生 Hook 的生命周期句柄。
/// 持有 <see cref="HashlinkHooks.HookHandle"/>，Dispose 时禁用。
/// </summary>
internal sealed class RawHookLease(HashlinkHooks.HookHandle handle) : IDisposable
{
    private HashlinkHooks.HookHandle? handle = handle;

    /// <summary>
    /// 是否已经禁用。
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.IsDisposed)
        {
            return;
        }

        this.IsDisposed = true;
        HashlinkHooks.HookHandle? current = this.handle;
        this.handle = null;
        current?.Disable();
    }
}
