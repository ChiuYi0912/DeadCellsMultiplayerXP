using DeadCellsMultiplayerX.Server.World.Actors;
namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline
{
    internal sealed class BindingStage : IActionStage
    {
        private readonly WorldState world;
        private readonly SpawnSynchronizer spawns;

        public BindingStage(
            WorldState world,
            SpawnSynchronizer spawns)
        {
            this.world = world;
            this.spawns = spawns;
        }

        public StageResult Process(ActionContext context)
        {
            var request = context.Request!;
            var actor = context.Actor ?? world.GetOrCreate(context.SenderGuid);

            bool created = actor.EnsureGhost(
                context.Game!,
                context.Level!,
                context.Snapshot?.Pos?.CX ?? 0,
                context.Snapshot?.Pos?.CY ?? 0);

            if (actor.Ghost == null)
                return StageResult.Reject(Rejection.GhostUnavailable);

            if (created)
                spawns.Sync(actor);

            actor.Admission.Commit(request.ActionId);

            if (!created)
                spawns.Sync(actor);

            context.Actor = actor;
            return StageResult.Continue;
        }
    }

    /// <summary>
    /// 写入并执行动作
    /// </summary>
}
