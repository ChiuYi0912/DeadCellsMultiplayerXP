namespace DeadCellsMultiplayerX.Server.World.Heroes.Actions.Pipeline;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.World.Actors;

internal sealed class PublicationStage(WorldState world) : IActionStage
{
    private readonly WorldState world = world;

    public StageResult Process(ActionContext context)
    {
        var request = context.Request!;
        var ghost = context.Ghost!;

        this.world.RecordEvent(new HeroActionEvent
        {
            Epoch = request.Epoch,
            ActionId = request.ActionId,
            CasterGuid = ghost.OwnerGuid,
            Kind = request.Kind,
            Slot = request.Slot,
            Item = request.Item,
            CX = request.CX,
            CY = request.CY,
            XR = request.XR,
            YR = request.YR,
            Dir = request.Dir,
            DX = request.DX,
            DY = request.DY,
            Ratio = context.Ratio,
            Cycle = request.Cycle,
            BrutalityTier = ghost.Hero.brutalityTier,
            SurvivalTier = ghost.Hero.survivalTier,
            TacticTier = ghost.Hero.tacticTier,
            LevelId = context.LevelId,
            SubLevelIndex = context.SubLevelIndex,
            ServerTimeMs = context.ServerTimeMs,
            ShieldKind = request.ShieldKind,
            FullParry = request.FullParry,
            SourceNetId = request.SourceNetId,
            SourceActionId = request.SourceActionId,
        });

        return StageResult.Continue;
    }
}
