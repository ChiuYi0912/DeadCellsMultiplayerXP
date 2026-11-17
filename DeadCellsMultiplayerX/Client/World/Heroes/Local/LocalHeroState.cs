namespace DeadCellsMultiplayerX.Client.World.Heroes.Local;

using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Engine.Utilities;

/// <summary>Read-only local hero observations. Capturing never changes native game state.</summary>
internal sealed class LocalHeroState
{
    public bool HasHero { get; private set; }
    public bool IsAlive { get; private set; }
    public bool InCinematic { get; private set; }
    public string CurrentLevelId { get; private set; } = string.Empty;
    public int SubLevelIndex { get; private set; } = -1;
    public bool InSubLevel { get; private set; }

    internal void Capture(Game? game, Hero? hero, bool allowLevelFallback)
    {
        this.HasHero = hero != null && !hero.destroyed;
        this.IsAlive = this.HasHero && hero is { life: > 0 };
        this.InCinematic = game?.hasCinematic() ?? false;

        var level = hero?._level ?? (allowLevelFallback ? game?.curLevel : null);
        // During transitions, post-update keeps the last known level until Hero._level is ready.
        if (level == null)
        {
            return;
        }
        this.CurrentLevelId = level.map?.id?.ToString() ?? string.Empty;
        try
        {
            this.SubLevelIndex = level.GetSubLevelIndex();
        }
        catch
        {
            this.SubLevelIndex = -1;
        }
        var levels = game?.subLevels;
        var mainLevel = levels != null && levels.length > 0 ? levels.getDyn(0) : null;
        this.InSubLevel = mainLevel != null && level != mainLevel;
    }
}
