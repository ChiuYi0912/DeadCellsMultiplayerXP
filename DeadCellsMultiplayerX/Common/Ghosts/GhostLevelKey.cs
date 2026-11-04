using dc.pr;
using DeadCellsMultiplayerX.Utils;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// Ghost 所在关卡的稳定标识（levelId + 子关卡索引）。
    /// </summary>
    internal readonly record struct GhostLevelKey(
        string LevelId,
        int SubLevelIndex)
    {
        public bool IsValid
            => SubLevelIndex >= 0 && !string.IsNullOrEmpty(LevelId);

        public static GhostLevelKey From(Level? level)
        {
            if (level == null)
                return default;

            return new GhostLevelKey(
                level.map?.id?.ToString() ?? string.Empty,
                level.GetSubLevelIndex());
        }

        public bool Matches(Level? level)
        {
            if (level == null) return false;

            return LevelId == (level.map?.id?.ToString() ?? string.Empty)
                && SubLevelIndex == level.GetSubLevelIndex();
        }

        public bool Matches(GhostLevelKey other)
            => LevelId == other.LevelId
                && SubLevelIndex == other.SubLevelIndex;
    }
}
