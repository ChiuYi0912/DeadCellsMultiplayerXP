namespace DeadCellsMultiplayerX.Engine.Hooks.Shared
{
    /// <summary>
    /// 事件式 Hook 的退订句柄。
    /// Dispose 幂等；用于替代直接丢弃订阅返回值。
    /// </summary>
    internal sealed class HookLease : IDisposable
    {
        private Action? unsubscribe;
        private bool disposed;

        public HookLease(Action? unsubscribe)
        {
            this.unsubscribe = unsubscribe;
        }

        /// <summary>
        /// 是否已经退订。
        /// </summary>
        public bool IsDisposed => disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Action? action = unsubscribe;
            unsubscribe = null;
            action?.Invoke();
        }
    }
}
