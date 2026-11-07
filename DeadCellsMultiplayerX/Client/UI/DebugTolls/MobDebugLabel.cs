using dc;
using dc.h2d;
using HaxeProxy.Runtime;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.UI.DebugTools
{
    public class MobDebugLabel
    {
        private Entity mob;

        public MobDebugLabel(Entity mob)
        {
            this.mob = mob;
            mob.setGameplayLabel($"HP={mob.life}/{mob.maxLife} state=chase", default);
        }
    }
}