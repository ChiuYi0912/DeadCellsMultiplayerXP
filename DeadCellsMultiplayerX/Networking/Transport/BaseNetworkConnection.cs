using DeadCellsMultiplayerX.Foundation.Lifecycle;
namespace DeadCellsMultiplayerX.Networking.Transport
{
    internal abstract class BaseNetworkConnection : Disposable
    {
        public abstract Stream Stream { get; }
        public abstract Task Init(CancellationToken cancellationToken);
    }
}
