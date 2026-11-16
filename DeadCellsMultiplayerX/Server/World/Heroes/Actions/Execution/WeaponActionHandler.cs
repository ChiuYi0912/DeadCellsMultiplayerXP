namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

internal sealed class WeaponActionHandler : IHeroActionModule
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "WeaponActionHandler");

    public HeroActionRules Rules { get; } =
        new(HeroActionKind.Weapon, SlotPolicy.Weapon);

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
        var command = context.Command;
        var slot = command.Slot;
        var cycle = command.Intent?.Cycle ?? 0;

        if (!ghost.Actions.PrepareWeapon(slot, item))
        {
            Logger.Warning(
                "Weapon rejected: guid={Guid} slot={Slot} item={Item} " +
                "cmd={CommandId} reason=prepare",
                ghost.OwnerGuid, slot, item.ItemId, command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        if (!ghost.Actions.UseWeapon(slot, cycle))
        {
            Logger.Warning(
                "Weapon rejected: guid={Guid} slot={Slot} item={Item} " +
                "cycle={Cycle} cmd={CommandId} reason=use",
                ghost.OwnerGuid, slot, item.ItemId, cycle, command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        Logger.Information(
            "Weapon use accepted: guid={Guid} slot={Slot} item={Item} " +
            "cycle={Cycle} cmd={CommandId}",
            ghost.OwnerGuid, slot, item.ItemId, cycle, command.CommandId);

        return HeroRejection.None;
    }
}
