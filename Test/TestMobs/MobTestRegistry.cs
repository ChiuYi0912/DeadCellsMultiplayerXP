using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using TestMobs;

namespace Test.TestMobs
{
    public class MobTestRegistry :
    IEventReceiver,
    IOnFrameUpdate
    {
        private readonly Dictionary<Type, MobTestBase> byType = new();
        private readonly List<MobTestBase> all = new();
        public IReadOnlyList<MobTestBase> All => all;

        public MobTestRegistry()
        {
            EventSystem.AddReceiver(this);
        }

        public void Register(MobTestBase test) => all.Add(test);

        public void Register<T>(MobTestBase<T> test, bool isCurrentTest = false) where T : dc.en.Mob
        {
            Register((MobTestBase)test);

            if (isCurrentTest)
                test.OpenTestKey = true;
        }

        public void InitAllHooks()
        {
            foreach (var t in all)
            {
                t.InitHook();
            }
        }

        public void DisposeAllHooks()
        {
            foreach (var t in all)
            {
                t.DisposeHook();
            }
            Clear();
        }

        public void Clear()
        {
            byType.Clear();
            all.Clear();
        }

        void IOnFrameUpdate.OnFrameUpdate(double dt)
        {
            foreach (var item in all)
                item.Update();
        }
    }

}