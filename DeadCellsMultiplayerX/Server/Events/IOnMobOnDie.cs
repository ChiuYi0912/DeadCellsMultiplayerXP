using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModCore.Events;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    internal interface IOnMobOnDie
    {
        void MobOnDie(dc.en.Mob mob);
    }
}