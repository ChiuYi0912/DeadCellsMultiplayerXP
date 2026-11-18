namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using dc.en;
using dc.pr;

/// <summary>Guest-only native corrections, kept separate from state observation and action recording.</summary>
internal static class LocalHeroGameRules
{
    public static void Apply(Game game, Hero hero, Func<Hero, bool> canRepairTargetability)
    {
        if (hero.destroyed || !ReferenceEquals(hero, game.hero))
        {
            return;
        }
        var team = game.curLevel?.teamHero;
        if (team != null)
        {
            if (hero._team != team)
            {
                hero._team = team;
            }
            var found = false;
            var members = team.members;
            if (members != null)
            {
                for (var i = 0; i < members.length; i++)
                {
                    if (ReferenceEquals(members.getDyn(i), hero))
                    {
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
            {
                team.addMember(hero);
            }
        }
        if (!hero._targetable && hero.life > 0 && !game.hasCinematic() && !game.paused
            && canRepairTargetability(hero))
        {
            hero.set_targetable(true);
        }
    }
}
