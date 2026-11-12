namespace DeadCellsMultiplayerX.Foundation.Lifecycle;

using Microsoft;
using Serilog;

/// <summary>
/// Deterministic lifetime base.  Disposal is explicit and idempotent; there is
/// no finalizer, because finalizer-thread cleanup of game/network objects is
/// both unsafe and non-deterministic.  Resource owners that hold async
/// resources override <see cref="MyDisposeAsync"/> and are disposed through
/// <see cref="DisposeAsync"/> by their owner.
/// </summary>
public abstract class Disposable : IDisposable, IAsyncDisposable, IDisposableObservable
{
    private readonly CancellationTokenSource disposedSource = new();
    private int disposed;

    public CancellationToken DisposeToken => this.disposedSource.Token;

    public bool IsDisposed => Volatile.Read(ref this.disposed) != 0;

    public virtual ILogger Logger { get; }

    protected abstract void MyDispose();

    protected Disposable() => this.Logger = Log.ForContext(this.GetType());

    public void Dispose()
    {
        if (!this.TryBeginDispose())
        {
            return;
        }

        GC.SuppressFinalize(this);
        this.MyDispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!this.TryBeginDispose())
        {
            return;
        }

        GC.SuppressFinalize(this);
        await this.MyDisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Async cleanup hook.  The default preserves the synchronous contract;
    /// resource owners override this when an owned resource only supports
    /// <see cref="IAsyncDisposable"/> (e.g. QUIC).
    /// </summary>
    protected virtual ValueTask MyDisposeAsync()
    {
        this.MyDispose();
        return default;
    }

    /// <summary>
    /// Marks the object disposed exactly once and cancels <see cref="DisposeToken"/>.
    /// Returns false when another thread already started disposal.
    /// </summary>
    protected bool TryBeginDispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return false;
        }

        this.disposedSource.Cancel();
        return true;
    }

    /// <summary>
    /// Sync fallback for callers that cannot await.  Runs the async disposal
    /// on the thread pool so it cannot capture and deadlock the game context.
    /// </summary>
    protected static void DisposeAsyncBlocking(Func<ValueTask> disposeAsync)
    {
        ArgumentNullException.ThrowIfNull(disposeAsync);
        Task.Run(async () => await disposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }

    /// <summary>
    /// Disposes one owned resource without letting a failure skip the rest of
    /// the owner chain.
    /// </summary>
    protected void DisposeSafely(IDisposable? resource, string label)
    {
        if (resource == null)
        {
            return;
        }

        try
        {
            resource.Dispose();
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Dispose failed: {Resource}", label);
        }
    }

    /// <summary>
    /// Async variant of <see cref="DisposeSafely"/>.
    /// </summary>
    protected async ValueTask DisposeSafelyAsync(
        Func<ValueTask> disposeAsync,
        string label)
    {
        try
        {
            await disposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Dispose failed: {Resource}", label);
        }
    }
}
