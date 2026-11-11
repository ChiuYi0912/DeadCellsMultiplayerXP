using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay.Presenters
{
    internal interface IActionPresenter
    {
        HeroActionKind Kind { get; }

        bool Present(
            RemoteGhostManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientGhostHero ghost);
    }
}
