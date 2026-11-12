using dc;
using dc.tool.atk;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Simulation.Actions;
using DeadCellsMultiplayerX.Server.World.Heroes.Actions.Modules;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using Serilog;
using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Execution
{
    internal sealed class ShieldActionHandler : IServerActionModule
    {
        private const long COUNTER_DEDUPE_MS = 250;

        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "ServerActorManager");

        private readonly ServerEntityRegistry entities;
        private readonly Func<long> clock;
        private readonly Dictionary<(string Guid, uint NetId), long> lastCounters = [];

        public ShieldActionHandler(
            ServerEntityRegistry entities,
            Func<long> clock)
        {
            this.entities = entities;
            this.clock = clock;
        }

        public ActionDescriptor Descriptor { get; } =
            new(HeroActionKind.Shield, SlotPolicy.Weapon);

        public bool TryExecute(
            ServerHeroCombatProxy ghost,
            HeroAction request,
            double ratio)
        {
            if (!ghost.Actions.PrepareShield(request.Slot, request.Item))
            {
                Logger.Warning(
                    "Shield rejected: guid={Guid} slot={Slot} item={Item} " +
                    "action={ActionId}",
                    ghost.OwnerGuid, request.Slot,
                    request.Item?.ItemId, request.ActionId);
                return false;
            }

            AttackData? sourceAtk = null;

            if (request.ShieldKind == ShieldActionKind.Counter)
            {
                if (request.SourceNetId == 0
                    || !entities.TryGetMob(request.SourceNetId, out var mob)
                    || mob == null
                    || mob.destroyed)
                {
                    Logger.Warning(
                        "Shield counter rejected: guid={Guid} source={Source} " +
                        "action={ActionId}",
                        ghost.OwnerGuid, request.SourceNetId, request.ActionId);
                    return false;
                }

                var key = (ghost.OwnerGuid, request.SourceNetId);
                long now = Environment.TickCount64;
                if (lastCounters.TryGetValue(key, out long last)
                    && now - last < COUNTER_DEDUPE_MS)
                {
                    return false;
                }

                lastCounters[key] = now;

                sourceAtk = new AttackData();
                sourceAtk.source = mob;
                sourceAtk.hitResult = new HitResult.Normal();
            }

            double elapsedMs = System.Math.Max(
                0, clock() - request.ClientTimeMs);

            if (!ghost.Actions.UseShield(
                    request.ShieldKind, request.Slot, ratio,
                    request.FullParry, sourceAtk, elapsedMs))
            {
                Logger.Warning(
                    "Shield action rejected: guid={Guid} kind={Kind} slot={Slot} " +
                    "action={ActionId}",
                    ghost.OwnerGuid, request.ShieldKind,
                    request.Slot, request.ActionId);
                return false;
            }

            Logger.Information(
                "Shield action accepted: guid={Guid} kind={Kind} slot={Slot} " +
                "fullParry={FullParry} source={Source} action={ActionId}",
                ghost.OwnerGuid, request.ShieldKind, request.Slot,
                request.FullParry, request.SourceNetId, request.ActionId);

            return true;
        }
    }
}
