using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;
namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes
{
    /// <summary>
    /// 服务端/客户端 Ghost 实体的公共只读契约。
    /// </summary>
    internal interface IGhostEntity : IDisposable
    {
        string OwnerGuid { get; }

        Beheaded Hero { get; }

        bool IsAlive { get; }

        LevelKey LevelKey { get; }

        bool MatchesLevel(Level? level);

        void SetTransform(
            int cx, int cy, double xr, double yr, int dir, double dx, double dy);

        void SetTiers(int brutality, int survival, int tactic);
    }
}
