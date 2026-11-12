namespace DeadCellsMultiplayerX.Client.Session;

using DeadCellsMultiplayerX.Foundation.Lifecycle;

public abstract class ClientBase : DisposableEventReceiver
{

    protected override void MyDispose()
    {
        this.Logger.Information("Disposing client...");
        base.MyDispose();
    }
}
