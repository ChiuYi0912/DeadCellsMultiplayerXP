using System;
using dc.en.hero;
using dc.pr;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    /// <summary>
    /// 服务端/客户端 Ghost 实体的公共只读契约。
    /// </summary>
    internal interface IGhostEntity : IDisposable
    {
        string OwnerGuid { get; }

        Beheaded Hero { get; }

        bool IsAlive { get; }

        GhostLevelKey LevelKey { get; }

        bool MatchesLevel(Level? level);

        void SetTransform(
            int cx, int cy, double xr, double yr, int dir, double dx, double dy);

        void SetTiers(int brutality, int survival, int tactic);
    }
}
