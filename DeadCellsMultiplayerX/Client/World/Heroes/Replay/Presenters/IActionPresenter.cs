namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters;

using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;

internal interface IActionPresenter
{
    public HeroActionKind Kind { get; }

    public bool Present(
        ClientReplayProxyManager manager,
        WorldFrame frame,
        HeroActionEvent evt,
        ClientHeroReplayProxy ghost);
}
