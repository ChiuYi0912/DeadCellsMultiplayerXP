using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using Serilog;
using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution
{
    internal sealed class TalismanActionHandler : IServerActionModule
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "ServerActorManager");

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Talisman, SlotPolicy.TalismanOnly);

        public bool TryExecute(
            ServerHeroCombatProxy ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.ApplyTalisman(request.Item))
            {
                Logger.Warning(
                    "Talisman rejected: guid={Guid} item={Item} action={ActionId}",
                    ghost.OwnerGuid, request.Item.ItemId, request.ActionId);
                return false;
            }

            Logger.Information(
                "Talisman accepted: guid={Guid} item={Item} action={ActionId}",
                ghost.OwnerGuid, request.Item.ItemId, request.ActionId);

            return true;
        }
    }
}
