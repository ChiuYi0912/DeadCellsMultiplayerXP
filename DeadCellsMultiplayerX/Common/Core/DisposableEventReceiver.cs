using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Core
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
