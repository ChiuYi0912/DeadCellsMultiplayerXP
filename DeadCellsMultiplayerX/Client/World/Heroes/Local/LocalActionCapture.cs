namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Engine.Hooks.Client;

/// <summary>
/// 本地动作捕获的组合根：持有命令队列、Hook 生命周期与三个协作者。
/// </summary>
internal sealed class LocalActionCapture(
    GuestGameSession session) : IDisposable
{
    private LocalIntentRecorder? recorder;
    private IDisposable? hooks;
    private bool disposed;

    internal HeroCommandQueue Commands { get; } = new();

    public void Init()
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);
        if (this.hooks != null)
        {
            return;
        }

        this.recorder ??= new LocalIntentRecorder(
            session,
            new HeroCommandSender(this.Commands),
            new LocalActionDeduper());

        this.hooks = ClientActionHookBinding.Install(this.recorder);
    }

    internal void ObserveEpoch(int epoch)
    {
        if (this.Commands.ObserveEpoch(epoch))
        {
            this.recorder?.Clear();
        }
    }

    internal void DebugCastSkill(string itemId, int slot)
        => this.recorder?.DebugCastSkill(itemId, slot);

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        => this.recorder?.DebugUseWeapon(itemId, slot, cycle);

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        try
        {
            this.hooks?.Dispose();
        }
        finally
        {
            this.hooks = null;
            this.Commands.Clear();
            this.recorder?.Clear();
            this.recorder = null;
        }
    }
}
