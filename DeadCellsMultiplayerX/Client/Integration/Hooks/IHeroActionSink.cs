using DeadCellsMultiplayerX.Contracts.Actions;

namespace DeadCellsMultiplayerX.Client.Integration.Hooks
{
    /// <summary>
    /// 本地动作上报通道；由会话实现，采集层不直接依赖 RPC 契约。
    /// </summary>
    internal interface IHeroActionSink
    {
        Task SubmitHeroAction(HeroAction action);
    }
}
