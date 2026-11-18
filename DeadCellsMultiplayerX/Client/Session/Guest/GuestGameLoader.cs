namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc;
using dc.pr;
using Serilog;
using Microsoft.VisualStudio.Threading;

/// <summary>Loads the native save and waits for game initialization. ClientWorld owns takeover.</summary>
internal sealed class GuestGameLoader(SynchronizationContext gameContext, ILogger logger)
{
    public async Task<Game> LoadSavedGameAsync(CancellationToken cancellationToken)
    {
        await gameContext;
        cancellationToken.ThrowIfCancellationRequested();
        logger.Information("Loading guest game...");
        Main.Class.ME.options.disableLoreRooms = true;
        Main.Class.ME.cleanUser();
        Main.Class.ME.launchGame(new LaunchMode.LoadSave(), null, null);

        Game loadedGame;
        while (true)
        {
            await Task.Delay(1, cancellationToken);
            await gameContext;
            cancellationToken.ThrowIfCancellationRequested();
            loadedGame = Game.Class.ME;
            if (loadedGame?.curLevel != null && loadedGame.subLevels != null
                && loadedGame.hero != null && !loadedGame.hero.destroyed && loadedGame.hero.initDone
                && !Main.Class.ME.isLoading)
            {
                break;
            }
        }

        return loadedGame;
    }
}
