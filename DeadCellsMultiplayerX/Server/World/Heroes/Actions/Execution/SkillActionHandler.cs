using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using Serilog;
using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution
{
    internal sealed class SkillActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.ActiveSkill, SlotPolicy.Active);

        public bool TryExecute(
            ServerHeroCombatProxy ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.PrepareSkill(request.Slot, request.Item))
                return false;

            if (!ghost.Actions.IsSkillReady(request.Slot))
            {
                Logger.Warning(
                    "Reject skill cast: guid={Guid} slot={Slot} not ready",
                    ghost.OwnerGuid, request.Slot);
                return false;
            }

            if (!ghost.Actions.CastSkill(request.Slot, ratio))
                return false;

            Logger.Information(
                "Skill cast accepted: guid={Guid} slot={Slot} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Slot, request.Item.ItemId, request.ActionId);

            return true;
        }
    }

    /// <summary>
    /// 服务端使用武器模块。
    /// 思路：Prepare -> Use，只负责在服务端 Ghost 上执行一次武器；
    /// 延迟补偿（MobRewind）由 ExecutionStage 统一包裹，模块本身不处理时钟回退。
    /// </summary>
}
