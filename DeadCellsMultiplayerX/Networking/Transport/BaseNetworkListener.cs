namespace DeadCellsMultiplayerX.Networking.Transport;

using DeadCellsMultiplayerX.Foundation.Lifecycle;

internal abstract class BaseNetworkListener : Disposable
{
    public abstract Task Init();
    public abstract Task<BaseNetworkConnection> WaitConnect(CancellationToken cancellationToken);
}
