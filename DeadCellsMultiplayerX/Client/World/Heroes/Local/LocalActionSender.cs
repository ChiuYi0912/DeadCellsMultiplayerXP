namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 本地 HeroAction 的发送通道；只负责提交与观察故障。
/// </summary>
internal sealed class LocalActionSender(IHeroActionSink actionSink)
{
    private readonly IHeroActionSink actionSink = actionSink
            ?? throw new ArgumentNullException(nameof(actionSink));

    public void Send(HeroAction request)
    {
        var task = this.actionSink.SubmitHeroAction(request);
        _ = task.ContinueWith(
            static t => _ = t.Exception,
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
