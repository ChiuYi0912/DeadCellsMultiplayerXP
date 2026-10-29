using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModCore.Events;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Core.Event
{
    [Event]
    public interface IOnGuestAttachMob
    {
        public record class Data(dc.level.Mob mobdata, dc.en.Mob mob);
        public void OnAttachMob(Data data);
    }
}