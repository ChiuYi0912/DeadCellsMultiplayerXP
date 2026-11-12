namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using Serilog;

/// <summary>
/// 战斗结算 Hook 路由。
/// </summary>
internal sealed class ServerCombatHookRouter : IServerCombatHookSink
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ServerCombatHookRouter");

    public void OnApplyHitResult(
        Hook__AttackUtils.orig_applyHitResult orig,
        AttackData atk,
        Entity target)
    {
#if DEBUG
        var gameHero = dc.pr.Game.Class.ME?.hero;
        if (atk?.source is Hero hero
            && !ReferenceEquals(hero, gameHero)
            && target is Mob mob)
        {
            Logger.Information(
                "Ghost attack hit: source={Source} target={Target} " +
                "finalDmg={FinalDmg} hit={HitResult} targetLife={Life}",
                hero.type, mob.GetType().Name,
                atk.finalDmg, atk.hitResult, mob.life);
        }
#endif
        orig(atk, target);
    }
}
