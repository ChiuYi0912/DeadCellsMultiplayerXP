namespace DeadCellsMultiplayerX.Client.World.Mobs.Replication;

using DeadCellsMultiplayerX.Contracts.Entities;
using DeadCellsMultiplayerX.Contracts.Snapshots;

internal static class MobFrameQueries
{
    public static EntityDynamic? FindMob(WorldFrame frame, uint netId)
    {
        if (frame?.Entities == null)
        {
            return null;
        }

        foreach (var entity in frame.Entities)
        {
            if (entity != null
                && entity.Kind == EntityKind.Mob
                && entity.NetId == netId)
            {
                return entity;
            }
        }

        return null;
    }

    public static bool ContainsDespawn(WorldFrame frame, uint netId)
    {
        if (frame?.DespawnedEntities == null)
        {
            return false;
        }

        foreach (var despawn in frame.DespawnedEntities)
        {
            if (despawn != null && despawn.NetId == netId)
            {
                return true;
            }
        }

        return false;
    }
}
