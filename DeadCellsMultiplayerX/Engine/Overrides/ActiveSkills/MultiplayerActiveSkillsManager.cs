namespace DeadCellsMultiplayerX.Engine.Overrides.ActiveSkills;

using dc.en;
using dc.tool.hero.activeSkills;

internal abstract class MultiplayerActiveSkillsManager(Hero hero)
    : BeheadedActiveSkillsManager(hero ?? throw new ArgumentNullException(nameof(hero)))
{
}
