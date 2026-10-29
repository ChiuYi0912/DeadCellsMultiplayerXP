using DeadCellsMultiplayerX.Common.Core;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Networks
{
    internal abstract class BaseNetworkListener : Disposable
    {
        public abstract Task Init();
        public abstract Task<BaseNetworkConnection> WaitConnect(CancellationToken cancellationToken);
    }
}
