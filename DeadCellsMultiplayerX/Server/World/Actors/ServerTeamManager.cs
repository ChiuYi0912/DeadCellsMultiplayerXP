using dc;
using dc.pr;
using DeadCellsMultiplayerX.Server.Replication.Frames;

namespace DeadCellsMultiplayerX.Server.World.Actors
{
    /// <summary>
    /// 服务端 Ghost 队伍同步入口：编排生成、状态镜像与战斗视图更新。
    /// </summary>
    internal sealed class ServerTeamManager
    {
        /// <summary>
        /// 客户端停止上传状态多久后视为离线，关闭该 Ghost 的战斗状态
        /// </summary>
        private const long STALE_STATE_MS = 2000;

        /// <summary>
        /// 客户端上传的帧数据，提供每个 guid 的英雄状态
        /// </summary>
        private readonly ClientFrameCollector frames;

        /// <summary>
        /// 获取当前 Game 实例
        /// </summary>
        private readonly Func<Game?> gameProvider;

        private readonly ServerHeroSpawner spawner;
        private readonly ServerCombatViewUpdater views = new();

        public ServerTeamManager(
            WorldState world,
            ClientFrameCollector frames,
            SpawnSynchronizer spawns,
            Func<Game?> gameProvider)
        {
            this.frames = frames;
            this.gameProvider = gameProvider;
            spawner = new ServerHeroSpawner(world, spawns, gameProvider);
        }

        /// <summary>
        /// 当前可战斗 Ghost
        /// </summary>
        public IReadOnlyCollection<ServerHeroCombatView> Combatants
            => views.Combatants;

        /// <summary>
        /// 用客户端最新上传状态同步所有 Ghost
        /// </summary>
        public void Sync(IReadOnlyCollection<string> guests, long nowMs)
        {
            var game = gameProvider();
            if (game?.curLevel == null || game.hero == null)
                return;

            // 服务端自己的 Hero 没有玩家控制，不能参与 mob 仇恨
            views.NeutralizeServerHero(game);

            foreach (string guid in guests)
            {
                if (string.IsNullOrEmpty(guid)) continue;

                if (!frames.TryGetHeroState(guid, out var state))
                {
                    // 本地 Hero 死亡后客户端停止上传，服务端要把目标关闭
                    if (views.TryGet(guid, out var stale)
                        && stale.LastStateMs > 0
                        && nowMs - stale.LastStateMs > STALE_STATE_MS)
                    {
                        views.Disable(guid);
                    }

                    continue;
                }

                if (state.Pos == null) continue;

                var actor = spawner.Ensure(game, guid, state, out var level);
                if (actor?.Ghost == null || level == null)
                {
                    views.Disable(guid);
                    continue;
                }

                bool combat = ServerHeroStateMirror.Apply(actor.Ghost, state);
                views.Update(
                    guid, actor.Ghost, level, state, combat, nowMs);
            }
        }

        /// <summary>
        /// 按 GUID 获取可战斗 Ghost
        /// </summary>
        public bool TryGet(string guid, out ServerHeroCombatView combatant)
            => views.TryGet(guid, out combatant);

        /// <summary>
        /// 通过底层 Hero 反查可战斗 Ghost
        /// </summary>
        public bool TryGet(Entity entity, out ServerHeroCombatView combatant)
            => views.TryGet(entity, out combatant);

        /// <summary>
        /// 断线清理
        /// </summary>
        public void Forget(string guid)
            => views.Forget(guid);

        /// <summary>
        /// 世界重置
        /// </summary>
        public void Clear()
            => views.Clear();
    }
}
