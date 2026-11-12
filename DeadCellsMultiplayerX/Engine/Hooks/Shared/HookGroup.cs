namespace DeadCellsMultiplayerX.Engine.Hooks.Shared;

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
        {
            return;
        }

        if (this.disposed)
        {
            lease.Dispose();
            return;
        }

        this.leases.Add(lease);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        for (var i = this.leases.Count - 1; i >= 0; i--)
        {
            this.leases[i].Dispose();
        }

        this.leases.Clear();
    }
}
