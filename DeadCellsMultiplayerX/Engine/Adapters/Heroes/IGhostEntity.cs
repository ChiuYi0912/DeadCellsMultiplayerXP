namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Contracts.Entities;

/// <summary>
/// 服务端/客户端 Ghost 实体的公共只读契约。
/// </summary>
internal interface IGhostEntity : IDisposable
{
    public string OwnerGuid { get; }

    public Beheaded Hero { get; }

    public bool IsAlive { get; }

    public LevelKey LevelKey { get; }

    public bool MatchesLevel(Level? level);

    public void SetTransform(
        int cx, int cy, double xr, double yr, int dir, double dx, double dy);

    public void SetTiers(int brutality, int survival, int tactic);
}
