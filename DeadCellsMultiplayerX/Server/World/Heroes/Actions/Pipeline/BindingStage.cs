namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline;

using DeadCellsMultiplayerX.Server.World.Actors;

internal sealed class BindingStage(
    WorldState world,
    SpawnSynchronizer spawns) : IActionStage
{
    private readonly WorldState world = world;
    private readonly SpawnSynchronizer spawns = spawns;

    public StageResult Process(ActionContext context)
    {
        var request = context.Request!;
        var actor = context.Actor ?? this.world.GetOrCreate(context.SenderGuid);

        var created = actor.EnsureGhost(
            context.Game!,
            context.Level!,
            context.Snapshot?.Pos?.CX ?? 0,
            context.Snapshot?.Pos?.CY ?? 0);

        if (actor.Ghost == null)
        {
            return StageResult.Reject(Rejection.GhostUnavailable);
        }

        if (created)
        {
            this.spawns.Sync(actor);
        }

        actor.Admission.Commit(request.ActionId);

        if (!created)
        {
            this.spawns.Sync(actor);
        }

        context.Actor = actor;
        return StageResult.Continue;
    }
}

/// <summary>
/// 写入并执行动作
/// </summary>

