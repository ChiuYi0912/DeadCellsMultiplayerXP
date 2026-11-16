namespace DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 一组 Hook 租约；按相反顺序释放。
/// </summary>
internal sealed class HookGroup : IDisposable
{
    private readonly List<IDisposable> leases = [];
    private bool disposed;

    /// <summary>Records rollback before installing a subscription.</summary>
    public void Subscribe(Action subscribe, Action unsubscribe)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        this.Add(new HookLease(unsubscribe));
        subscribe();
    }

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
        List<Exception>? errors = null;
        for (var i = this.leases.Count - 1; i >= 0; i--)
        {
            try
            {
                this.leases[i].Dispose();
            }
            catch (Exception ex) { (errors ??= []).Add(ex); }
        }

        this.leases.Clear();
        if (errors != null)
        {
            throw new AggregateException("Hook cleanup failed", errors);
        }
    }
}
