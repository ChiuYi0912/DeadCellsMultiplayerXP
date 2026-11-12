namespace DeadCellsMultiplayerX.Networking.Transport;

using DeadCellsMultiplayerX.Foundation.Lifecycle;

internal abstract class BaseNetworkConnection : Disposable
{
    public abstract Stream Stream { get; }
    public abstract Task Init(CancellationToken cancellationToken);
}
