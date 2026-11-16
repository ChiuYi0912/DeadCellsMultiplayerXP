namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;

/// <summary>
/// 一种英雄动作的完整实现：静态规则、无副作用校验、有副作用执行。
/// </summary>
internal interface IHeroActionModule
{
    public HeroActionRules Rules { get; }

    /// <summary>
    /// 只读校验；不得修改引擎状态。
    /// </summary>
    public HeroRejection Validate(
        HeroCommandContext context,
        ItemRef item);

    /// <summary>
    /// 执行动作；返回拒绝原因时由 dispatcher 统一生成 outcome。
    /// </summary>
    public HeroRejection Execute(
        HeroCommandContext context,
        ItemRef item);
}
