namespace DeadCellsMultiplayerX.Engine.Hooks.Shared
{
    /// <summary>
    /// 一组 Hook 租约；按相反顺序释放。
    /// </summary>
    internal sealed class HookGroup : IDisposable
    {
        private readonly List<IDisposable> leases = [];
        private bool disposed;

        public void Add(IDisposable lease)
        {
            if (lease == null)
                return;

            if (disposed)
            {
                lease.Dispose();
                return;
            }

            leases.Add(lease);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            for (int i = leases.Count - 1; i >= 0; i--)
                leases[i].Dispose();
            leases.Clear();
        }
    }
}
