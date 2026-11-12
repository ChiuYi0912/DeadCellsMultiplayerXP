using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Snapshots;

namespace DeadCellsMultiplayerX.Server.World.Actors
{
    /// <summary>
    /// 维护 guid / Hero 到可战斗视图的索引，并更新客户端镜像状态。
    /// </summary>
    internal sealed class ServerCombatViewUpdater
    {
        private readonly Dictionary<string, ServerHeroCombatView> byGuid = [];
        private readonly Dictionary<Hero, ServerHeroCombatView> byHero = [];

        public IReadOnlyCollection<ServerHeroCombatView> Combatants
            => byGuid.Values;

        public bool TryGet(string guid, out ServerHeroCombatView combatant)
            => byGuid.TryGetValue(guid ?? string.Empty, out combatant!);

        public bool TryGet(Entity entity, out ServerHeroCombatView combatant)
        {
            if (entity is Hero hero
                && byHero.TryGetValue(hero, out combatant!))
                return true;

            combatant = null!;
            return false;
        }

        public void Update(
            string guid,
            ServerHeroCombatProxy ghost,
            Level level,
            HeroDynamic state,
            bool combat,
            long nowMs)
        {
            var view = GetOrCreate(guid, ghost, level);
            view.Update(
                level,
                state.Life,
                state.MaxLife,
                state.IsDead,
                combat,
                nowMs);
        }

        /// <summary>
        /// 关闭指定 Ghost 的 mob 可选中状态。
        /// </summary>
        public void Disable(string guid)
        {
            if (!byGuid.TryGetValue(guid ?? string.Empty, out var combatant))
                return;

            combatant.DisableCombat();
            combatant.Ghost.SetCombatEnabled(false);
        }

        /// <summary>
        /// 断线清理。
        /// </summary>
        public void Forget(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            if (!byGuid.Remove(guid, out var combatant)) return;

            byHero.Remove(combatant.Ghost.Hero);
            combatant.Ghost.SetCombatEnabled(false);
        }

        /// <summary>
        /// 世界重置。
        /// </summary>
        public void Clear()
        {
            foreach (var combatant in byGuid.Values)
                combatant.Ghost.SetCombatEnabled(false);

            byGuid.Clear();
            byHero.Clear();
        }

        /// <summary>
        /// 将服务端的 Hero 设置为不可攻击状态。
        /// </summary>
        public void NeutralizeServerHero(Game game)
        {
            var hero = game?.hero;
            if (hero == null || hero.destroyed)
                return;

            if (byHero.ContainsKey(hero))
                return;

            if (hero._targetable)
                hero.set_targetable(false);

            if (!hero.ignoreDetection)
                hero.ignoreDetection = true;
        }

        private ServerHeroCombatView GetOrCreate(
            string guid, ServerHeroCombatProxy ghost, Level level)
        {
            if (byGuid.TryGetValue(guid, out var existing))
            {
                if (ReferenceEquals(existing.Ghost, ghost))
                    return existing;

                // 切关卡会重建 GhostHero，旧实体不能继续留在查询表里
                byHero.Remove(existing.Ghost.Hero);
                existing.Ghost.SetCombatEnabled(false);
            }

            var combatant = new ServerHeroCombatView(guid, ghost, level);
            byGuid[guid] = combatant;
            byHero[ghost.Hero] = combatant;
            return combatant;
        }
    }
}
