namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

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
        {
            return false;
        }

        if (!ghost.Actions.IsSkillReady(request.Slot))
        {
            Logger.Warning(
                "Reject skill cast: guid={Guid} slot={Slot} not ready",
                ghost.OwnerGuid, request.Slot);
            return false;
        }

        if (!ghost.Actions.CastSkill(request.Slot, ratio))
        {
            return false;
        }

        Logger.Information(
            "Skill cast accepted: guid={Guid} slot={Slot} item={Item} action={ActionId}",
            ghost.OwnerGuid, request.Slot, request.Item.ItemId, request.ActionId);

        return true;
    }
}


