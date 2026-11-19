namespace DeadCellsMultiplayerX.Server.Integration;

using dc;
using DeadCellsMultiplayerX.Engine.Adapters.Levels;
using dc.cine;
using dc.pr;
using Microsoft.VisualStudio.Threading;
using ModCore.Utilities;
using Serilog;

/// <summary>Performs native loading and saving; cancellation and operation ownership belong to WorldCoordinator.</summary>
internal sealed class ServerGameLoader(
    SynchronizationContext gameContext, Thread gameThread, string savePath) : IDisposable
{
    private readonly ILogger logger = Log.ForContext<ServerGameLoader>();
    private Game? enteredGame;
    private string? enteredLevelId;
    private LevelEntrySubscription? entry;
    private LevelTransition? enteredTransition;

    internal void OnLevelLoading(LevelTransition transition, string levelId)
    {
        this.VerifyGameThread();
        this.ResetObservation();
        this.entry = new(transition, () =>
        {
            this.VerifyGameThread();
            this.enteredGame = Game.Class.ME;
            this.enteredTransition = transition;
            this.enteredLevelId = levelId;
        });
    }

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

    internal void ResetObservation()
    {
        this.VerifyGameThread();
        this.enteredGame = null;
        this.enteredLevelId = null;
        this.enteredTransition = null;
        this.entry?.Dispose();
        this.entry = null;
    }

    public void Dispose() => this.ResetObservation();

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
            // Entry identifies this operation; native Hero initialization can finish later.
            if (game?.curLevel == null || game.subLevels == null
                || game.hero == null || game.hero.destroyed || !game.hero.initDone
                || Main.Class.ME.isLoading || !ReferenceEquals(game, this.enteredGame)
                || (expectedTransition != null && !ReferenceEquals(expectedTransition, this.enteredTransition))
                || (expectedLevelId != null && !string.Equals(expectedLevelId, this.enteredLevelId, StringComparison.Ordinal)))
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
