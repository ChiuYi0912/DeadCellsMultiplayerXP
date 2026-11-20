namespace DeadCellsMultiplayerX.Engine.Overrides.Weapons;

using dc.en;
using dc.tool.hero.weap;

internal abstract class MultiplayerWeaponsManager(Hero hero)
    : BeheadedWeaponsManager(hero ?? throw new ArgumentNullException(nameof(hero)))
{
}
