using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters
{
    internal interface IActionPresenter
    {
        HeroActionKind Kind { get; }

        bool Present(
            ClientReplayProxyManager manager,
            WorldFrame frame,
            HeroActionEvent evt,
            ClientHeroReplayProxy ghost);
    }
}
