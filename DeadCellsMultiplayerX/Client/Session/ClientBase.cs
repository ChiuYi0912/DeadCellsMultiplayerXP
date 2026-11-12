using DeadCellsMultiplayerX.Foundation.Lifecycle;
namespace DeadCellsMultiplayerX.Client.Session
{
    public abstract class ClientBase : DisposableEventReceiver
    {

        protected override void MyDispose()
        {
            Logger.Information("Disposing client...");
            base.MyDispose();
        }
    }
}
