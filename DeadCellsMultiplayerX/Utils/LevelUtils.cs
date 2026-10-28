using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using System;
using System.Collections.Generic;
using System.Text;

namespace DeadCellsMultiplayerX.Utils
{
    internal static class LevelUtils
    {
        public static int GetSubLevelIndex(this Level lvl, Game? game = null)
        {
            game ??= lvl.game;

            int subLevelId = 0;

            for (int i = 0; i < game.subLevels.length; i++)
            {
                if ((Level)game.subLevels.getDyn(i) == lvl)
                {
                    subLevelId = i;
                }
            }
            return subLevelId;
        }

        /// <summary>
        /// 按 level id 和子关卡索引查找本地 Level
        /// </summary>
        public static Level? ResolveLevel(
            Game? game, string levelId, int subLevelIndex)
        {
            if (game == null) return null;

            var current = game.curLevel;
            if (Matches(current, levelId, subLevelIndex))
                return current;

            var subLevels = game.subLevels;
            if (subLevels == null) return null;

            for (int i = 0; i < subLevels.length; i++)
            {
                if (subLevels.getDyn(i) is Level level
                    && Matches(level, levelId, subLevelIndex))
                {
                    return level;
                }
            }

            return null;
        }

        /// <summary>
        /// 判断 Level 是否匹配 level id 和子关卡索引
        /// </summary>
        public static bool Matches(
            Level? level, string levelId, int subLevelIndex)
        {
            if (level == null) return false;

            return (level.map?.id?.ToString() ?? string.Empty) == levelId
                && level.GetSubLevelIndex() == subLevelIndex;
        }

        public static bool IsLocalLevel(Hero hero, GhostHitEvent hit)
        {
            var level = hero._level;
            if (level == null)
                return false;

            string levelId = level.map?.id?.ToString() ?? string.Empty;
            return string.Equals(levelId, hit.LevelId, StringComparison.Ordinal)
                && level.GetSubLevelIndex() == hit.SubLevelIndex;
        }
    }
}
