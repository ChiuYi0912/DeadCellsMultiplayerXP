namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 本地动作捕获的组合根：持有命令队列、Hook 生命周期与三个协作者。
/// </summary>
internal sealed class LocalActionCapture(
    GuestClientSession session) : IDisposable
{
    private LocalIntentRecorder? recorder;
    private HookGroup? hookGroup;

    internal HeroCommandQueue Commands { get; } = new();

    public void Init()
    {
        this.recorder ??= new LocalIntentRecorder(
            session,
            new HeroCommandSender(this.Commands),
            new LocalActionDeduper());

        this.hookGroup = new HookGroup();
        this.hookGroup.Add(ClientActionHookBinding.Install(this.recorder));
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
        this.hookGroup?.Dispose();
        this.hookGroup = null;
        this.Commands.Clear();
        this.recorder?.Clear();
        this.recorder = null;
    }
}
