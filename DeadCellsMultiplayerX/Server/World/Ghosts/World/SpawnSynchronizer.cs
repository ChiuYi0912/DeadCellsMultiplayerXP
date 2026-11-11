using System.Collections.Generic;
using DeadCellsMultiplayerX.Server.World.Replication.Frames;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.World
{
    internal sealed class SpawnSynchronizer
    {
        private readonly ClientWorldFrameCollector frames;

        public SpawnSynchronizer(ClientWorldFrameCollector frames)
        {
            this.frames = frames;
        }

        /// <summary>
        /// spawn 版本变化时才应用项链
        /// </summary>
        public void Sync(Actor actor)
        {
            if (actor.Ghost == null)
                return;

            if (!frames.TryGetHeroSpawn(actor.OwnerGuid, out var spawn))
                return;

            if (!actor.NeedsSpawn(spawn.Version))
                return;

            if (spawn.Loadout != null)
                actor.Ghost.Actions.ApplyLoadout(spawn.Loadout);
            else
                actor.Ghost.Actions.ApplyTalisman(spawn.Talisman);

            actor.MarkSpawnApplied(spawn.Version);
        }

        /// <summary>
        /// 同步当前世界中所有 ServerGhostHero
        /// </summary>
        public void SyncAll(IEnumerable<Actor> actors)
        {
            foreach (var actor in actors)
                Sync(actor);
        }
    }
}
