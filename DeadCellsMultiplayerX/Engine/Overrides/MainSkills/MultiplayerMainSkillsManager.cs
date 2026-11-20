namespace DeadCellsMultiplayerX.Engine.Overrides.MainSkills;

using dc.en;
using dc.pr;
using dc.tool.hero.mainSkills;

internal abstract class MultiplayerMainSkillsManager(Hero hero, Game game)
    : BeheadedMainSkillsManager(
        hero ?? throw new ArgumentNullException(nameof(hero)),
        game ?? throw new ArgumentNullException(nameof(game)))
{
}
