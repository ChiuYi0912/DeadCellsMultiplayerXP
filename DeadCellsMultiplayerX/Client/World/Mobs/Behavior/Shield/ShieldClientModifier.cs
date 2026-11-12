namespace DeadCellsMultiplayerX.Client.World.Mobs.Behavior.Shield;

using dc.hl.types;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs.Behavior;
using Serilog;
using GameShield = dc.en.mob.Shield;

internal sealed class ShieldClientModifier : MobModifierBase<GameShield>
{
    private static readonly ILogger Logger = Log.ForContext("SourceContext", "ShieldClientModifier");

    /// <inheritdoc />
    public override void InitHook(IMobHookRegistry hooks)
    {
        hooks.OnInitSkills(this.OnInitSkills);

        if (hooks is IClientMobHooks clientHooks)
        {
            clientHooks.OnClientEventHandled(_ => false);
        }
    }

    private void OnInitSkills(
        MobHookContext context, ArrayObj oldSkills, ArrayObj skills)
    {

    }
}
