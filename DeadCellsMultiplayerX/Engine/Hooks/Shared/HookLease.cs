namespace DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 事件式 Hook 的退订句柄。
/// Dispose 幂等；用于替代直接丢弃订阅返回值。
/// </summary>
internal sealed class HookLease(Action? unsubscribe) : IDisposable
{
    private Action? unsubscribe = unsubscribe;

    /// <summary>
    /// 是否已经退订。
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
        Action? action = this.unsubscribe;
        this.unsubscribe = null;
        action?.Invoke();
    }
}
