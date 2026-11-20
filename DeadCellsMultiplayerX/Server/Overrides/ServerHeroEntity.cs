namespace DeadCellsMultiplayerX.Server.Overrides;

using dc.pr;
using DeadCellsMultiplayerX.Engine.Overrides;
using DeadCellsMultiplayerX.Server.Overrides.ActiveSkills;
using DeadCellsMultiplayerX.Server.Overrides.MainSkills;
using DeadCellsMultiplayerX.Server.Overrides.Weapons;

internal sealed class ServerHeroEntity : MultiplayerHero
{
    public ServerHeroEntity(Game game) : base(game)
    {
        this.weaponsManager = new ServerWeaponsManager(this);
        this.mainSkillsManager = new ServerMainSkillsManager(this, game);
        this.activeSkillsManager = new ServerActiveSkillsManager(this);
    }

}
