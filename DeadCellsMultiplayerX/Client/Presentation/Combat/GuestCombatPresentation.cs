namespace DeadCellsMultiplayerX.Client.Presentation.Combat;

using dc.en;
using dc.tool.atk;
using Serilog;

/// <summary>
/// Executes guest-side hit visuals and authorized native deaths.
/// Replication decides when a mob should die; this component owns the native execution guards.
/// </summary>
internal sealed class GuestCombatPresentation(ILogger logger)
{
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly CombatDeathGate deathGate = new();
    private int replayVisualDepth;

    public bool IsNativeDeathAllowed(Mob mob) => this.deathGate.IsAllowed(mob);

    public void ClearDeathPermissions() => this.deathGate.Clear();

    public void PlayHitVisual(AttackData atk, Mob mob)
    {
        if (mob == null || mob.destroyed || this.replayVisualDepth != 0)
        {
            return;
        }

        this.logger.Verbose(
            "Puppet hit visual: target={Target} dmg={Dmg} hit={Hit}",
            mob.type, atk.finalDmg, atk.hitResult);

        this.replayVisualDepth++;
        try
        {
            mob.onDamage(atk);
        }
        finally
        {
            this.replayVisualDepth--;
        }
    }

    public void ForceNativeDeath(Mob mob)
    {
        if (mob == null || mob.destroyed)
        {
            return;
        }

        using (this.deathGate.Allow(mob))
        {
            mob.life = 0;
            mob.onDie();
        }

        if (!mob.destroyed)
        {
            mob.destroy();
        }
    }
}
