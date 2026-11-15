namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Simulation.Actions;
using Serilog;

internal sealed class WeaponActionHandler : IServerActionModule
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

    public ActionDescriptor Descriptor { get; } =
        new(HeroActionKind.Weapon, SlotPolicy.Weapon);

    public bool TryExecute(
        ServerHeroCombatProxy ghost,
        HeroAction request,
        double ratio)
    {
        if (!ghost.Actions.PrepareWeapon(request.Slot, request.Item))
        {
            return false;
        }

        if (!ghost.Actions.UseWeapon(request.Slot, request.Cycle))
        {
            return false;
        }

        Logger.Information(
            "Weapon use accepted: guid={Guid} slot={Slot} item={Item} cycle={Cycle} action={ActionId}",
            ghost.OwnerGuid, request.Slot, request.Item.ItemId,
            request.Cycle, request.ActionId);

        return true;
    }
}

