namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc;
using dc.en;
using dc.pr;
using Serilog;
using Microsoft.VisualStudio.Threading;

/// <summary>Loads the native game and removes local mobs. The session owns replicated state.</summary>
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

        List<Entity> entities = [];
        for (var i = 0; i < loadedGame.subLevels.length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var level = loadedGame.subLevels.getDyn(i);
            for (var j = 0; j < level.entities.length; j++)
            {
                var entity = level.entities.getDyn(j);
                if (entity is Mob)
                {
                    entities.Add(entity);
                }
            }
            foreach (var entity in entities)
            {
                entity.destroy();
            }

            entities.Clear();
        }
        return loadedGame;
    }
}
