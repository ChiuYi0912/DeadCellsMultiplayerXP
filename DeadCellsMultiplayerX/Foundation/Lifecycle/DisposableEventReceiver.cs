using ModCore.Events;
namespace DeadCellsMultiplayerX.Foundation.Lifecycle
{
    public abstract class DisposableEventReceiver : Disposable, IEventReceiver
    {
        public DisposableEventReceiver()
        {
            EventSystem.AddReceiver(this);
        }

        protected override void MyDispose()
        {
            EventSystem.RemoveReceiver(this);
        }
    }
}
