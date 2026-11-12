using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Contracts.Actions;

namespace DeadCellsMultiplayerX.Client.World.Heroes.Local
{
    /// <summary>
    /// 本地 HeroAction 的发送通道；只负责提交与观察故障。
    /// </summary>
    internal sealed class LocalActionSender
    {
        private readonly IHeroActionSink actionSink;

        public LocalActionSender(IHeroActionSink actionSink)
        {
            this.actionSink = actionSink
                ?? throw new ArgumentNullException(nameof(actionSink));
        }

        public void Send(HeroAction request)
        {
            var task = actionSink.SubmitHeroAction(request);
            _ = task.ContinueWith(
                static t => _ = t.Exception,
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
