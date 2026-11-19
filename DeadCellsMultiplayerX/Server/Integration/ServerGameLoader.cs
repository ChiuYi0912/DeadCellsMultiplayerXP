namespace DeadCellsMultiplayerX.Server.Integration;

using dc;
using dc.cine;
using dc.pr;
using Microsoft.VisualStudio.Threading;
using ModCore.Utilities;
using Serilog;

/// <summary>Performs native loading and saving; cancellation and operation ownership belong to WorldCoordinator.</summary>
internal sealed class ServerGameLoader(
    SynchronizationContext gameContext, Thread gameThread, string savePath)
{
    private readonly ILogger logger = Log.ForContext<ServerGameLoader>();
    private Game? loadedGame;
    private LevelTransition? loadedTransition;
    private string? loadedLevelId;

    public async Task<byte[]> LoadSavedGameAsync(byte[] data, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(savePath, data, cancellationToken).ConfigureAwait(false);
        await gameContext;
        this.VerifyGameThread();
        cancellationToken.ThrowIfCancellationRequested();
        this.logger.Information("Launching server game from {Path}", savePath);
        Main.Class.ME.launchGame(new LaunchMode.NewGame(false, false), null, null);
        await this.WaitForGameReadyAsync(null, null, cancellationToken);
        await gameContext;
        this.VerifyGameThread();
        cancellationToken.ThrowIfCancellationRequested();
        Main.Class.ME.writeSave();
        while (Main.Class.ME.waitToSave)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            await gameContext;
            this.VerifyGameThread();
            cancellationToken.ThrowIfCancellationRequested();
            Main.Class.ME.writeSave();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return await File.ReadAllBytesAsync(savePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task EnterNextLevelAsync(string levelId, CancellationToken cancellationToken)
    {
        await gameContext;
        this.VerifyGameThread();
        cancellationToken.ThrowIfCancellationRequested();
        this.logger.Information("Server entering next level: {LevelId}", levelId);
        var transition = LevelTransition.Class.@goto.Invoke(levelId.AsHaxeString());
        await this.WaitForGameReadyAsync(transition, levelId, cancellationToken);
        this.logger.Information("Server entered next level: {LevelId}", levelId);
    }

    internal void OnMainLevelLoaded(Game game, LevelTransition transition, string levelId)
    {
        this.VerifyGameThread();
        this.loadedGame = game;
        this.loadedTransition = transition;
        this.loadedLevelId = levelId;
    }

    internal void ResetObservation()
    {
        this.VerifyGameThread();
        this.loadedGame = null;
        this.loadedTransition = null;
        this.loadedLevelId = null;
    }

    private async Task WaitForGameReadyAsync(
        LevelTransition? expectedTransition, string? expectedLevelId, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            await gameContext;
            this.VerifyGameThread();
            cancellationToken.ThrowIfCancellationRequested();
            var game = Game.Class.ME;
            // W0: native loading may finish before Hero.initDone; Game/Hero may be reused.
            if (game?.curLevel == null || game.subLevels == null
                || game.hero == null || game.hero.destroyed || !game.hero.initDone
                || Main.Class.ME.isLoading || !ReferenceEquals(game, this.loadedGame)
                || (expectedTransition != null && !ReferenceEquals(expectedTransition, this.loadedTransition))
                || (expectedLevelId != null && !string.Equals(expectedLevelId, this.loadedLevelId, StringComparison.Ordinal)))
            {
                continue;
            }
            this.ResetObservation();
            return;
        }
    }

    private void VerifyGameThread()
    {
        if (Thread.CurrentThread != gameThread)
            throw new InvalidOperationException("Native server game operations must run on the game thread.");
    }
}
