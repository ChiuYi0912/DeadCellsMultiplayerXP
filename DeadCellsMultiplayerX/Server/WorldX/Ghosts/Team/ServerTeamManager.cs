using System;
using System.Collections.Generic;
using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.World;
using DeadCellsMultiplayerX.Server.WorldX.Hubs;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Team
{
    /// <summary>
    /// 服务端 Ghost 队伍与生命周期管理
    /// </summary>
    internal sealed class ServerTeamManager
    {
        /// <summary>
        /// 客户端停止上传状态多久后视为离线，关闭该 Ghost 的战斗状态
        /// </summary>
        private const long StaleStateMs = 2000;

        /// <summary>
        /// Ghost 运行时状态
        /// </summary>
        private readonly WorldState world;

        /// <summary>
        /// 客户端上传的帧数据，提供每个 guid 的英雄状态
        /// </summary>
        private readonly ClientWorldFrameCollector frames;

        /// <summary>
        /// Ghost 生成/销毁同步器
        /// </summary>
        private readonly SpawnSynchronizer spawns;

        /// <summary>
        /// 获取当前 Game 实例
        /// </summary>
        private readonly Func<Game?> gameProvider;

        /// <summary>
        /// 可战斗 Ghost 视图
        /// </summary>
        private readonly Dictionary<string, GhostCombatant> byGuid = [];

        /// <summary>
        /// 可战斗 Ghost 视图，用于从 mob 目标反查,不在词典中的 Hero 就是服务端的主 Hero
        /// </summary>
        private readonly Dictionary<Hero, GhostCombatant> byHero = [];


        public ServerTeamManager(
            WorldState world,
            ClientWorldFrameCollector frames,
            SpawnSynchronizer spawns,
            Func<Game?> gameProvider)
        {
            this.world = world;
            this.frames = frames;
            this.spawns = spawns;
            this.gameProvider = gameProvider;
        }

        /// <summary>
        /// 当前可战斗 Ghost
        /// </summary>
        public IReadOnlyCollection<GhostCombatant> Combatants => byGuid.Values;

        /// <summary>
        /// 用客户端最新上传状态同步所有 Ghost
        /// </summary>
        public void Sync(IReadOnlyCollection<string> guests, long nowMs)
        {
            var game = gameProvider();
            if (game?.curLevel == null || game.hero == null)
                return;

            // 服务端自己的 Hero 没有玩家控制，不能参与 mob 仇恨
            NeutralizeServerHero(game);

            foreach (string guid in guests)
            {
                if (string.IsNullOrEmpty(guid)) continue;

                if (!frames.TryGetHeroState(guid, out var state))
                {
                    // 本地 Hero 死亡后客户端停止上传，服务端要把目标关闭
                    if (byGuid.TryGetValue(guid, out var stale)
                        && stale.LastStateMs > 0
                        && nowMs - stale.LastStateMs > StaleStateMs)
                    {
                        stale.DisableCombat();
                        stale.Ghost.SetCombatEnabled(false);
                    }

                    continue;
                }

                if (state.Pos == null) continue;

                var level = LevelUtils.ResolveLevel(
                    game, state.LevelId, state.SubLevelIndex);

                if (level == null)
                {
                    DisableCombat(guid);
                    continue;
                }

                var actor = world.GetOrCreate(guid);
                bool created = actor.EnsureGhost(
                    game, level, state.Pos.CX, state.Pos.CY);

                if (actor.Ghost == null)
                {
                    DisableCombat(guid);
                    continue;
                }

                actor.Ghost.SetTransform(
                    state.Pos.CX,
                    state.Pos.CY,
                    state.Pos.XR,
                    state.Pos.XY,
                    state.Pos.DIR,
                    state.Pos.DX,
                    state.Pos.DY);
                actor.Ghost.SyncLevel();
                actor.Ghost.SetServerVitals();

                bool combat = !state.IsDead && state.Life > 0;
                actor.Ghost.SetCombatEnabled(combat);

                if (created)
                    spawns.Sync(actor);

                var combatant = GetOrCreate(
                    guid, actor.Ghost, level);
                combatant.Update(
                    level,
                    state.Life,
                    state.MaxLife,
                    state.IsDead,
                    combat,
                    nowMs);
            }
        }

        /// <summary>
        /// 按 GUID 获取可战斗 Ghost
        /// </summary>
        public bool TryGet(string guid, out GhostCombatant combatant)
            => byGuid.TryGetValue(guid ?? string.Empty, out combatant!);

        /// <summary>
        /// 通过底层 Hero 反查可战斗 Ghost
        /// </summary>
        public bool TryGet(Entity entity, out GhostCombatant combatant)
        {
            if (entity is Hero hero
                && byHero.TryGetValue(hero, out combatant!))
                return true;

            combatant = null!;
            return false;
        }

        /// <summary>
        /// 断线清理
        /// </summary>
        public void Forget(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            if (!byGuid.Remove(guid, out var combatant)) return;

            byHero.Remove(combatant.Ghost.Hero);
            combatant.Ghost.SetCombatEnabled(false);
        }

        /// <summary>
        /// 世界重置
        /// </summary>
        public void Clear()
        {
            foreach (var combatant in byGuid.Values)
                combatant.Ghost.SetCombatEnabled(false);

            byGuid.Clear();
            byHero.Clear();
        }

        /// <summary>
        /// 取得 guid 对应的战斗视图；切关卡重建 Ghost 时替换旧实体
        /// </summary>
        private GhostCombatant GetOrCreate(
            string guid, GhostHero ghost, Level level)
        {
            if (byGuid.TryGetValue(guid, out var existing))
            {
                if (ReferenceEquals(existing.Ghost, ghost))
                    return existing;

                // 切关卡会重建 GhostHero，旧实体不能继续留在查询表里
                byHero.Remove(existing.Ghost.Hero);
                existing.Ghost.SetCombatEnabled(false);
            }

            var combatant = new GhostCombatant(guid, ghost, level);
            byGuid[guid] = combatant;
            byHero[ghost.Hero] = combatant;
            return combatant;
        }

        /// <summary>
        /// 关闭指定 Ghost 的 mob 可选中状态
        /// </summary>
        private void DisableCombat(string guid)
        {
            if (!byGuid.TryGetValue(guid, out var combatant))
                return;

            combatant.DisableCombat();
            combatant.Ghost.SetCombatEnabled(false);
        }

        /// <summary>
        /// 将服务端的hero设置为不可攻击状态
        /// </summary>
        /// <param name="game"></param>
        private void NeutralizeServerHero(Game game)
        {
            var hero = game.hero;
            if (hero == null || hero.destroyed)
                return;

            if (byHero.ContainsKey(hero))
                return;

            if (hero._targetable)
                hero.set_targetable(false);

            if (!hero.ignoreDetection)
                hero.ignoreDetection = true;
        }
    }
}
