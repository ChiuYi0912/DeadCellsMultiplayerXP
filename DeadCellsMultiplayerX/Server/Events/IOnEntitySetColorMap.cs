using dc;
using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    internal interface IOnEntitySetColorMap
    {
        public record class Data(Entity Entity, string Model, string Skin);
        public void OnEntitySetColorMap(Data data);
    }
}
