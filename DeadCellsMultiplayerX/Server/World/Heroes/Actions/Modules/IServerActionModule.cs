namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Simulation.Actions;

/// <summary>
/// 服务端动作模块的统一契约。
/// 思路：把“按 Kind 执行什么”从 ActionExecutor 的 switch/硬编码数组中拆出来，
/// 每个动作实现一个 module；执行器只按 Descriptor.Kind 查表，新增动作不需要改分发表。
/// </summary>
internal interface IServerActionModule
{
    public ActionDescriptor Descriptor { get; }

    public bool TryExecute(
        ServerHeroCombatProxy ghost,
        HeroAction request,
        double ratio);
}
