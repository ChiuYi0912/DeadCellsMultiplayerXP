using ModCore.Modules;

namespace DeadCellsMultiplayerX.Engine.Hooks.Shared
{
    /// <summary>
    /// Hashlink 原生 Hook 的生命周期句柄。
    /// 持有 <see cref="HashlinkHooks.HookHandle"/>，Dispose 时禁用。
    /// </summary>
    internal sealed class RawHookLease : IDisposable
    {
        private HashlinkHooks.HookHandle? handle;
        private bool disposed;

        public RawHookLease(HashlinkHooks.HookHandle handle)
        {
            this.handle = handle;
        }

        /// <summary>
        /// 是否已经禁用。
        /// </summary>
        public bool IsDisposed => disposed;

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            HashlinkHooks.HookHandle? current = handle;
            handle = null;
            current?.Disable();
        }
    }
}
