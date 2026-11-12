using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Engine.Utilities;

namespace DeadCellsMultiplayerX.Server.World.Actors
{
    /// <summary>
    /// 根据客户端上传状态解析关卡并确保 ServerHeroCombatProxy 存在。
    /// </summary>
    internal sealed class ServerHeroSpawner
    {
        private readonly WorldState world;
        private readonly SpawnSynchronizer spawns;
        private readonly Func<Game?> gameProvider;

        public ServerHeroSpawner(
            WorldState world,
            SpawnSynchronizer spawns,
            Func<Game?> gameProvider)
        {
            this.world = world
                ?? throw new ArgumentNullException(nameof(world));
            this.spawns = spawns
                ?? throw new ArgumentNullException(nameof(spawns));
            this.gameProvider = gameProvider
                ?? throw new ArgumentNullException(nameof(gameProvider));
        }

        /// <summary>
        /// 确保 guid 对应的 Ghost 在状态所在关卡存在；返回 null 表示当前不可创建。
        /// </summary>
        public Actor? Ensure(
            Game game, string guid, HeroDynamic state, out Level? level)
        {
            level = null;

            if (game == null || state?.Pos == null)
                return null;

            level = LevelUtils.ResolveLevel(
                game, state.LevelId, state.SubLevelIndex);
            if (level == null)
                return null;

            var actor = world.GetOrCreate(guid);
            bool created = actor.EnsureGhost(
                game, level, state.Pos.CX, state.Pos.CY);

            if (actor.Ghost == null)
                return null;

            if (created)
                spawns.Sync(actor);

            return actor;
        }
    }
}
