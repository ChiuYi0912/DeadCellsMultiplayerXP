namespace DeadCellsMultiplayerX.Tools.HeroSkillTest;

using dc.pr;
using ModCore.Events.Interfaces.Game.Hero;
using ModCore.Mods;

public class TestMain(ModInfo info) : ModBase(info),
IOnHeroInit
{
    public override void Initialize() => base.Initialize();

    void IOnHeroInit.OnHeroInit()
    {
        var hero = Game.Class.ME.hero;
        if (hero == null)
        {
            return;
        }
    }
}
