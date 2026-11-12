namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 本地动作上报通道；由会话实现，采集层不直接依赖 RPC 契约。
/// </summary>
internal interface IHeroActionSink
{
    public Task SubmitHeroAction(HeroAction action);
}
