namespace DeadCellsMultiplayerX.Server.World.Actors;

using DeadCellsMultiplayerX.Server.Replication.Frames;

internal sealed class SpawnSynchronizer(ClientFrameCollector frames)
{
    private readonly ClientFrameCollector frames = frames;

    /// <summary>
    /// spawn 版本变化时才应用项链
    /// </summary>
    public void Sync(Actor actor)
    {
        if (actor.Ghost == null)
        {
            return;
        }

        if (!this.frames.TryGetHeroSpawn(actor.OwnerGuid, out var spawn))
        {
            return;
        }

        if (!actor.NeedsSpawn(spawn.Version))
        {
            return;
        }

        if (spawn.Loadout != null)
        {
            actor.Ghost.Actions.ApplyLoadout(spawn.Loadout);
        }
        else
        {
            actor.Ghost.Actions.ApplyTalisman(spawn.Talisman);
        }

        actor.MarkSpawnApplied(spawn.Version);
    }

    /// <summary>
    /// 同步当前世界中所有 ServerHeroCombatProxy
    /// </summary>
    public void SyncAll(IEnumerable<Actor> actors)
    {
        foreach (var actor in actors)
        {
            this.Sync(actor);
        }
    }
}
