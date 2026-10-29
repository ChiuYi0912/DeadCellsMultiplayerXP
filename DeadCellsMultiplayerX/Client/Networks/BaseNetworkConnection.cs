using DeadCellsMultiplayerX.Common.Core;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Networks
{
    internal abstract class BaseNetworkConnection : Disposable
    {
        public abstract Stream Stream { get; }
        public abstract Task Init(CancellationToken cancellationToken);
    }
}
