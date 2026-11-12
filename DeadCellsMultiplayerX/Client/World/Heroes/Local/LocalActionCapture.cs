namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 本地动作捕获的组合根：持有 Hook 生命周期与三个协作者，
/// 捕获/去重/发送逻辑分别在 LocalIntentRecorder / LocalActionDeduper / LocalActionSender。
/// </summary>
internal sealed class LocalActionCapture(
    GuestClientSession session, IHeroActionSink actionSink) : IDisposable
{
    private readonly LocalIntentRecorder recorder = new LocalIntentRecorder(
            session,
            new LocalActionSender(actionSink),
            new LocalActionDeduper());
    private HookGroup? hookGroup;

    public void Init()
    {
        this.hookGroup = new HookGroup();
        this.hookGroup.Add(ClientActionHookBinding.Install(this.recorder));
    }

    internal void DebugCastSkill(string itemId, int slot)
        => this.recorder.DebugCastSkill(itemId, slot);

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        => this.recorder.DebugUseWeapon(itemId, slot, cycle);

    public void Dispose()
    {
        this.hookGroup?.Dispose();
        this.hookGroup = null;
        this.recorder.Clear();
    }
}
