using DeadCellsMultiplayerX.Foundation.Lifecycle;
namespace DeadCellsMultiplayerX.Networking.Transport
{
    internal abstract class BaseNetworkListener : Disposable
    {
        public abstract Task Init();
        public abstract Task<BaseNetworkConnection> WaitConnect(CancellationToken cancellationToken);
    }
}
