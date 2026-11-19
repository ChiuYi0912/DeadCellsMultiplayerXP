namespace DeadCellsMultiplayerX.Tools.MobTestMod;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using dc;
using Hashlink.Reflection.Types;
using ModCore.Events;
using Serilog;

public abstract class MobTestBase : IEventReceiver
{
    protected ILogger Logger { get; }
    public abstract System.Type MobType { get; }
    public virtual string Name => MobType.Name + "Test";
    public bool IsEnabled { get; set; }

    public MobTestBase()
    {
        EventSystem.AddReceiver(this);
        Logger = Log.ForContext(GetType());
    }

    public abstract void InitHook();
    public virtual void DisposeHook() { }
    public void Update()
    {
        if (!IsEnabled)
            return;

        OnFrameUpdate();
    }

    public virtual void OnFrameUpdate() { }
}
public abstract class MobTestBase<T> : MobTestBase
    where T : dc.en.Mob
{
    public sealed override System.Type MobType => typeof(T);
}
