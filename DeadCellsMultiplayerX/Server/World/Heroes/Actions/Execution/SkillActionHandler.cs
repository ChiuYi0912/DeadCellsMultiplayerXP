namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

internal sealed class SkillActionHandler : IHeroActionModule
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "SkillActionHandler");

    public HeroActionRules Rules { get; } =
        new(HeroActionKind.ActiveSkill, SlotPolicy.Active);

    public HeroRejection Validate(
        HeroCommandContext context,
        ItemRef item)
        => context.Ghost == null
            ? HeroRejection.GhostUnavailable
            : HeroRejection.None;

    public HeroRejection Execute(
        HeroCommandContext context,
        ItemRef item)
    {
        var ghost = context.Ghost!;
        var slot = context.Command.Slot;

        if (!ghost.Actions.PrepareSkill(slot, item))
        {
            Logger.Warning(
                "Skill rejected: guid={Guid} slot={Slot} item={Item} " +
                "cmd={CommandId} reason=prepare",
                ghost.OwnerGuid, slot, item.ItemId,
                context.Command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        if (!ghost.Actions.IsSkillReady(slot))
        {
            Logger.Warning(
                "Skill rejected: guid={Guid} slot={Slot} item={Item} " +
                "cmd={CommandId} reason=not-ready",
                ghost.OwnerGuid, slot, item.ItemId,
                context.Command.CommandId);
            return HeroRejection.NotReady;
        }

        if (!ghost.Actions.CastSkill(slot, context.Ratio))
        {
            Logger.Warning(
                "Skill rejected: guid={Guid} slot={Slot} item={Item} " +
                "cmd={CommandId} reason=cast",
                ghost.OwnerGuid, slot, item.ItemId,
                context.Command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        Logger.Information(
            "Skill cast accepted: guid={Guid} slot={Slot} item={Item} " +
            "cmd={CommandId}",
            ghost.OwnerGuid, slot, item.ItemId, context.Command.CommandId);

        return HeroRejection.None;
    }
}
