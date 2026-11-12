namespace DeadCellsMultiplayerX.Client.World.Heroes.Replay.Presenters;

using DeadCellsMultiplayerX.Contracts.Actions;

internal static class ActionPresenterCatalog
{
    public static Dictionary<HeroActionKind, IActionPresenter> CreateDefault()
    {
        var presenters = new Dictionary<HeroActionKind, IActionPresenter>();

        Add(new SkillActionPresenter());
        Add(new WeaponActionPresenter());
        Add(new TalismanActionPresenter());
        Add(new ShieldActionPresenter());

        return presenters;

        void Add(IActionPresenter presenter) => presenters.Add(presenter.Kind, presenter);
    }
}
