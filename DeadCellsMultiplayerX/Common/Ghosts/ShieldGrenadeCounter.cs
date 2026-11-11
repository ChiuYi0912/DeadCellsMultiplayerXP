using System;
using dc.en;
using dc.pr;
using dc.tool.weap;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
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

            Hero previous = game.hero;
            game.hero = owner;

            try
            {
                counter();
            }
            finally
            {
                game.hero = previous;
            }
        }
    }
}
