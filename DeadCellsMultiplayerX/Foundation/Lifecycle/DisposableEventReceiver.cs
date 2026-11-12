namespace DeadCellsMultiplayerX.Foundation.Lifecycle;

using ModCore.Events;

public abstract class DisposableEventReceiver : Disposable, IEventReceiver
{
    public DisposableEventReceiver() => EventSystem.AddReceiver(this);

    protected override void MyDispose() => EventSystem.RemoveReceiver(this);
}
