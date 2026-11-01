using dc.en.mob;
using Hashlink.Reflection.Types;
using ModCore.Events.Interfaces.Game;
using ModCore.Mods;
using ModCore.Utilities;
using TestMobs.Mobs;

namespace Test.TestMobs
{
    public class TestMobMod(ModInfo info) : ModBase(info),
        IOnGameExit
    {
        private MobTestRegistry registry = new();
        public override void Initialize()
        {
            Logger.Information("Initialize TestMobMod");
            

            registry.Register(new ZombieTest(), true);
            registry.InitAllHooks();
        }

        void IOnGameExit.OnGameExit() => registry.DisposeAllHooks();
    }
}