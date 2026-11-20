namespace DeadCellsMultiplayerX.Client.Overrides.MainSkills;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Engine.Overrides.MainSkills;

internal sealed class ClientMainSkillsManager(Hero hero, Game game)
    : MultiplayerMainSkillsManager(hero, game)
{
}
