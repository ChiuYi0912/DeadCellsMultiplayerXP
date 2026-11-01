using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestMobs
{
    using System;
    using dc;
    using Hashlink.Reflection.Types;
    using ModCore.Events;
    using Serilog;

    public abstract class MobTestBase : IEventReceiver
    {
        public readonly ILogger logger;
        public abstract System.Type MobType { get; }
        public virtual string Name => MobType.Name + "Test";
        public bool OpenTestKey = false;

        public MobTestBase()
        {
            EventSystem.AddReceiver(this);
            logger = Log.ForContext(GetType());
        }

        public abstract void InitHook();
        public virtual void DisposeHook() { }
        public void Update()
        {
            if (!OpenTestKey) return;
            
            OnFrameUpdate();
        }

        public virtual void OnFrameUpdate() { }
    }
    public abstract class MobTestBase<T> : MobTestBase
        where T : dc.en.Mob
    {
        public sealed override System.Type MobType => typeof(T);
    }

}