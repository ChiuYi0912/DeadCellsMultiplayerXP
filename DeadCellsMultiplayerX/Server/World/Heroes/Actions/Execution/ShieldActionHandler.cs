namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Commands;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

internal sealed class ShieldActionHandler(
    Func<long> clock) : IHeroActionModule
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ShieldActionHandler");

    private readonly Func<long> clock = clock;

    public HeroActionRules Rules { get; } =
        new(HeroActionKind.Shield, SlotPolicy.Weapon);

    public HeroRejection Validate(
        HeroCommandContext context,
        ItemRef item)
    {
        if (context.Ghost == null)
        {
            return HeroRejection.GhostUnavailable;
        }

        var intent = context.Command.Intent;
        if (intent == null
            || intent.ShieldKind > ShieldIntentKind.ParryEnd)
        {
            return HeroRejection.InvalidRequest;
        }

        return context.Ghost.ShieldPhases[context.Command.Slot]
            .CanTransition(intent.ShieldKind, context.ServerTimeMs)
                ? HeroRejection.None
                : HeroRejection.InvalidRequest;
    }

    public HeroRejection Execute(
        HeroCommandContext context,
        ItemRef item)
    {
        var ghost = context.Ghost!;
        var command = context.Command;
        var intent = command.Intent!;

        if (!ghost.Actions.PrepareShield(command.Slot, item))
        {
            Logger.Warning(
                "Shield rejected: guid={Guid} slot={Slot} item={Item} " +
                "cmd={CommandId} reason=prepare",
                ghost.OwnerGuid, command.Slot, item.ItemId,
                command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        var elapsedMs = System.Math.Max(
            0, this.clock() - command.ClientTimeMs);

        if (!ghost.Actions.UseShield(
                intent.ShieldKind, command.Slot, elapsedMs))
        {
            Logger.Warning(
                "Shield rejected: guid={Guid} kind={Kind} slot={Slot} " +
                "cmd={CommandId} reason=phase",
                ghost.OwnerGuid, intent.ShieldKind, command.Slot,
                command.CommandId);
            return HeroRejection.ExecutionFailed;
        }

        ghost.ShieldPhases[command.Slot].Commit(
            intent.ShieldKind, context.ServerTimeMs);

        Logger.Information(
            "Shield phase accepted: guid={Guid} kind={Kind} slot={Slot} " +
            "cmd={CommandId}",
            ghost.OwnerGuid, intent.ShieldKind, command.Slot,
            command.CommandId);

        return HeroRejection.None;
    }
}
