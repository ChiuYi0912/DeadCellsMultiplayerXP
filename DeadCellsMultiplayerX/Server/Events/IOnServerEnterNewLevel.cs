using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    internal interface IOnServerEnterNewLevel
    {
        public void OnServerEnterNewLevel();
    }
}
