namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Foundation.Lifecycle;

/// <summary>Owns local hero event subscriptions and coordinates observation before game-rule repair.</summary>
internal sealed class LocalHeroTracker : DisposableEventReceiver, IOnLocalHeroInitDone, IOnLocalHeroPostUpdate
{
    private readonly Func<Hero, bool> canRepairHeroTargetability;
#if DEBUG
    private string lastLevelSignature = string.Empty;
#endif
    public LocalHeroState State { get; } = new();

    public LocalHeroTracker(Func<Hero, bool> canRepairHeroTargetability)
    {
        try
        {
            this.canRepairHeroTargetability = canRepairHeroTargetability
                ?? throw new ArgumentNullException(nameof(canRepairHeroTargetability));
            var game = Game.Class.ME;
            this.State.Capture(game, game?.hero, allowLevelFallback: true);
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        var game = Game.Class.ME;
        if (this.IsDisposed || game == null || !ReferenceEquals(hero, game.hero))
        {
            return;
        }
        this.State.Capture(game, hero, allowLevelFallback: true);
    }

    void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
    {
        var game = Game.Class.ME;
        if (this.IsDisposed || game == null || !ReferenceEquals(hero, game.hero))
        {
            return;
        }
        this.State.Capture(game, hero, allowLevelFallback: false);
#if DEBUG
        var signature = $"{this.State.CurrentLevelId}#{this.State.SubLevelIndex}";
        if (signature != this.lastLevelSignature)
        {
            this.lastLevelSignature = signature;
            this.Logger.Information("Local level: {LevelId}#{SubLevelIndex} inSubLevel={InSubLevel}",
                this.State.CurrentLevelId, this.State.SubLevelIndex, this.State.InSubLevel);
        }
#endif
        if (!this.IsDisposed && ReferenceEquals(game, Game.Class.ME) && ReferenceEquals(hero, game.hero))
        {
            LocalHeroGameRules.Apply(game, hero, this.canRepairHeroTargetability);
        }
    }
}
