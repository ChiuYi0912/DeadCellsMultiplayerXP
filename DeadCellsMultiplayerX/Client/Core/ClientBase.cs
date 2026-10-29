using DeadCellsMultiplayerX.Common.Core;
using ModCore.Events;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Core
{
    public abstract class ClientBase : DisposableEventReceiver
    {

        protected override void MyDispose()
        {
            Logger.Information("Disposing client...");
        }
    }
}
