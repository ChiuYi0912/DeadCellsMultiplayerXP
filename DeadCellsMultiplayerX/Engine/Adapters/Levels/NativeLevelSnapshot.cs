namespace DeadCellsMultiplayerX.Engine.Adapters.Levels;

using DeadCellsMultiplayerX.Contracts.Snapshots;

/// <summary>Reads native level collision data without depending on a session or RPC contract.</summary>
internal static class NativeLevelSnapshot
{
    public static AreaInfo CollectArea(RectInt rect, int subLevelIndex)
    {
        if (rect.X < 0)
        {
            rect.X = 0;
        }
        if (rect.Y < 0)
        {
            rect.Y = 0;
        }
        var areaInfo = new AreaInfo()
        {
            Rect = rect
        };

        var lvl = (dc.pr.Level)dc.pr.Game.Class.ME.subLevels.getDyn(subLevelIndex);
        var map = lvl.map;

        areaInfo.Collision = new int[rect.Width * rect.Height];

        // 碰撞箱
        unsafe
        {
            var src = new Span<int>((void*)map.collisions.bytes, map.collisions.length);
            for (var y = 0; y < rect.Height; y++)
            {
                var dst = new Span<int>(areaInfo.Collision, y * rect.Width, rect.Width);
                src.Slice(((rect.Y + y) * map.wid) + rect.X, rect.Width).CopyTo(dst);
            }
        }

        return areaInfo;
    }
}
