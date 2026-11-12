namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using dc.pr;
using dc.tool.weap;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

internal static class ShieldGrenadeCounter
{
    public static void Run(BaseShield? shield, Action counter)
    {
        var game = Game.Class.ME;
        var owner = shield?.owner;

        if (game == null
            || owner == null
            || ReferenceEquals(game.hero, owner))
        {
            counter();
            return;
        }

        using (HeroContextScope.Enter(game, owner))
        {
            counter();
        }
    }
}
