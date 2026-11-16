namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Simulation.Actions;

/// <summary>
/// 动作模块的静态规则；由 dispatcher 在调用模块前消费。
/// </summary>
internal sealed class HeroActionRules(
    HeroActionKind kind,
    SlotPolicy slotPolicy)
{
    public HeroActionKind Kind { get; } = kind;

    public SlotPolicy SlotPolicy { get; } = slotPolicy;
}
