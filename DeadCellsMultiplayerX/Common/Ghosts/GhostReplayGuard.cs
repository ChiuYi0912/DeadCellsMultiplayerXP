using System.Collections.Generic;
using dc.en;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// 武器重放批次内的表现命中记录
    /// 避免 fallback 对已经触发过受击表现的 mob 重复调用 onDamage
    /// </summary>
    public static class GhostReplayGuard
    {
        /// <summary>
        /// 当前武器重放批次内已被表现命中的 mob
        /// </summary>
        [ThreadStatic]
        private static HashSet<Mob>? visualHitMobs;

        /// <summary>
        /// 开始一次武器重放的表现批次
        /// </summary>
        public static void BeginVisualBatch()
        {
            visualHitMobs = [];
        }

        /// <summary>
        /// 结束当前表现批次
        /// </summary>
        public static void EndVisualBatch()
        {
            visualHitMobs = null;
        }

        /// <summary>
        /// 记录本次批次内已经触发过受击表现的 mob
        /// </summary>
        public static void MarkVisualHit(Mob? mob)
        {
            if (mob != null) visualHitMobs?.Add(mob);
        }

        /// <summary>
        /// 判断该 mob 在本次批次内是否已经触发过受击表现
        /// </summary>
        public static bool WasVisualHit(Mob mob)
            => visualHitMobs?.Contains(mob) == true;
    }
}
