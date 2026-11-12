using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

namespace DeadCellsMultiplayerX.Client.World.Heroes.Local
{
    /// <summary>
    /// 本地动作捕获的组合根：持有 Hook 生命周期与三个协作者，
    /// 捕获/去重/发送逻辑分别在 LocalIntentRecorder / LocalActionDeduper / LocalActionSender。
    /// </summary>
    internal sealed class LocalActionCapture : IDisposable
    {
        private readonly LocalIntentRecorder recorder;
        private HookGroup? hookGroup;

        public LocalActionCapture(
            GuestClientSession session, IHeroActionSink actionSink)
        {
            recorder = new LocalIntentRecorder(
                session,
                new LocalActionSender(actionSink),
                new LocalActionDeduper());
        }

        public void Init()
        {
            hookGroup = new HookGroup();
            hookGroup.Add(ClientActionHookBinding.Install(recorder));
        }

        internal void DebugCastSkill(string itemId, int slot)
            => recorder.DebugCastSkill(itemId, slot);

        internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
            => recorder.DebugUseWeapon(itemId, slot, cycle);

        public void Dispose()
        {
            hookGroup?.Dispose();
            hookGroup = null;
            recorder.Clear();
        }
    }
}
