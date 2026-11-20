namespace DeadCellsMultiplayerX.Client.Overrides;

using dc.pr;
using DeadCellsMultiplayerX.Client.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Client.Overrides.MainSkills;
using DeadCellsMultiplayerX.Client.Overrides.Weapons;
using DeadCellsMultiplayerX.Engine.Overrides;

internal sealed class ClientHeroEntity : MultiplayerHero
{
    public ClientHeroEntity(Game game) : base(game)
    {
        this.weaponsManager = new ClientWeaponsManager(this);
        this.mainSkillsManager = new ClientMainSkillsManager(this, game);
        this.activeSkillsManager = new ClientActiveSkillsManager(this);
    }
}
