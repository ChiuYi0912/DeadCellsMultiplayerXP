using dc;
using Hashlink.Virtuals;
using ModCore.Events;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Events
{
    [Event]
    internal interface IOnEntitySetGlowData
    {
        public record class Data(Entity Entity, int Index,
            virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ GlowData);
        public void OnEntitySetGlowData(Data data);
    }
}
