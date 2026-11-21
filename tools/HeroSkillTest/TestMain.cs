namespace DeadCellsMultiplayerX.Tools.HeroSkillTest;

using dc.en.hero;
using dc.pr;
using DeadCellsMultiplayerX.Client.Overrides;
using DeadCellsMultiplayerX.Client.Overrides.Weapons;
using ModCore.Mods;

public class TestMain(ModInfo info) : ModBase(info)
{
    public override void Initialize()
    {
        base.Initialize();
        Hook__Beheaded.__constructor__ += this.Hook__Beheaded_Super;
        OverridesHooks.Install();
    }

    private void Hook__Beheaded_Super(Hook__Beheaded.orig___constructor__ orig, Beheaded arg1, Game game)
    {
        orig(arg1, game);
        arg1.weaponsManager = new ClientWeaponsManager(arg1);
    }
}
