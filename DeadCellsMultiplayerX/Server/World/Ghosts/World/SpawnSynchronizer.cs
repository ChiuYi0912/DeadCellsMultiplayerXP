using System.Collections.Generic;
using DeadCellsMultiplayerX.Server.World.Hubs;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Ghosts.World
{
    /// <summary>
    /// 用客户端最新 spawn 中的项链同步 GhostHero
    /// </summary>
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

            actor.Ghost.Actions.ApplyTalisman(spawn.Talisman);
            actor.MarkSpawnApplied(spawn.Version);
        }

        /// <summary>
        /// 同步当前世界中所有 GhostHero
        /// </summary>
        public void SyncAll(IEnumerable<Actor> actors)
        {
            foreach (var actor in actors)
                Sync(actor);
        }
    }
}
