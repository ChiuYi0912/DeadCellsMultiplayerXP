using System;
using System.Collections.Generic;
using DeadCellsMultiplayerX.Common.Data.Ghosts;

namespace DeadCellsMultiplayerX.Client.Heroes.GhostReplay.Presenters
{
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

            void Add(IActionPresenter presenter)
            {
                presenters.Add(presenter.Kind, presenter);
            }
        }
    }
}
