namespace DeadCellsMultiplayerX.Server.World.Heroes.Replication;

using DeadCellsMultiplayerX.Server.World.Replication.Frames;

internal sealed class SpawnSynchronizer(ClientFrameCollector frames)
{
    private readonly ClientFrameCollector frames = frames;

    /// <summary>
    /// spawn 版本变化时才应用项链
    /// </summary>
    public void Sync(ServerHero hero)
    {
        var ghost = hero.Ghost;
        if (ghost == null)
        {
            return;
        }

        if (!this.frames.TryGetHeroSpawn(hero.OwnerGuid, out var spawn))
        {
            return;
        }

        // 动作解析需要最新的装备快照；即使 spawn 已应用也要刷新镜像。
        hero.Loadout.Apply(spawn.Loadout);

        if (spawn.Loadout != null
            && (ghost.Hero.brutalityTier != spawn.Loadout.BrutalityTier
                || ghost.Hero.survivalTier != spawn.Loadout.SurvivalTier
                || ghost.Hero.tacticTier != spawn.Loadout.TacticTier))
        {
            ghost.SetTiers(
                spawn.Loadout.BrutalityTier,
                spawn.Loadout.SurvivalTier,
                spawn.Loadout.TacticTier);
        }

        if (!hero.NeedsSpawn(spawn.Version))
        {
            return;
        }

        if (spawn.Loadout != null)
        {
            ghost.Actions.ApplyLoadout(spawn.Loadout);
        }

        hero.MarkSpawnApplied(spawn.Version);
    }

    /// <summary>
    /// 同步当前世界中所有 ServerHeroCombatProxy
    /// </summary>
    public void SyncAll(IEnumerable<ServerHero> heroes)
    {
        foreach (var hero in heroes)
        {
            this.Sync(hero);
        }
    }
}
