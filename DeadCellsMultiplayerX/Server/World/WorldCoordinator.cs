namespace DeadCellsMultiplayerX.Server.World;

using dc.cine;
using dc.pr;
using DeadCellsMultiplayerX.Server.Integration;
using DeadCellsMultiplayerX.Server.Session;
using Microsoft.VisualStudio.Threading;
using Serilog;

/// <summary>
/// Owns the current world, loading and transitions. ServerMain owns this coordinator.
/// </summary>
internal sealed class WorldCoordinator : IDisposable, System.IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly ServerSession session;
    private readonly ServerGameLoader loader;
    private readonly string savePath;
    private readonly ILogger logger = Log.ForContext<WorldCoordinator>();
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken token;
    private readonly TaskCompletionSource saveAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? initialLoadTask;
    private Task? transitionTask;
    private Task? activeOperation;
    private TaskCompletionSource? stopped;

    public ServerWorld? CurrentWorld { get; private set; }

    public WorldCoordinator(ServerSession session, string savePath)
    {
        session.VerifyGameThread();
        this.session = session;
        this.savePath = savePath;
        this.loader = new(session.GameContext, Thread.CurrentThread, savePath);
        this.lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.DisposeToken);
        this.token = this.lifetime.Token;
        this.CurrentWorld = new(() => session.CurrentTimeStamp);
    }

    public Task LoadInitialGameAsync(byte[] data)
    {
        this.session.VerifyGameThread();
        this.ThrowIfStopping();
        if (this.initialLoadTask != null)
        {
            return this.initialLoadTask;
        }


        this.VerifyNoActiveOperation();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.activeOperation = this.initialLoadTask = completion.Task;
        // Publish ownership before native work can reenter shutdown.
        _ = this.CompleteOperationAsync(this.LoadInitialGameCoreAsync(data), completion);
        return completion.Task;
    }

    public ValueTask EnterNextLevelAsync(string levelId, CancellationToken cancellationToken)
    {
        this.session.VerifyGameThread();
        this.ThrowIfStopping();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(levelId);
        this.VerifyNoActiveOperation();
        if (this.initialLoadTask?.IsCompletedSuccessfully != true)
        {

            throw new InvalidOperationException("Initial server game loading has not completed.");
        }


        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.activeOperation = this.transitionTask = completion.Task;
        // Once accepted, only the coordinator lifetime cancels native work, not its caller's wait.
        _ = this.CompleteOperationAsync(this.EnterNextLevelCoreAsync(levelId), completion);
        return new(completion.Task);
    }

    private async Task LoadInitialGameCoreAsync(byte[] data)
    {
        var savedGame = await this.loader.LoadSavedGameAsync(data, this.token).ConfigureAwait(false);
        await this.session.GameContext;
        this.ThrowIfStopping();
        this.CurrentWorld!.BeginLevelEpoch();
        this.ThrowIfStopping();
        this.session.NotifyInitialLevel(savedGame);
        this.ThrowIfStopping();
        this.saveAvailable.TrySetResult();
    }

    private async Task EnterNextLevelCoreAsync(string levelId)
    {
        await this.loader.EnterNextLevelAsync(levelId, this.token);
        await this.session.GameContext;
        this.ThrowIfStopping();
        this.CurrentWorld!.BeginLevelEpoch();
        this.ThrowIfStopping();
        this.session.NotifyNextLevel(levelId);
        this.ThrowIfStopping();
    }

    // An I/O/native-operation failure is supervised here, outside deterministic transition steps.
    private async Task CompleteOperationAsync(Task operation, TaskCompletionSource completion)
    {
        try
        {
            await operation;
            this.token.ThrowIfCancellationRequested();
            completion.TrySetResult();
        }
        catch (OperationCanceledException) when (this.token.IsCancellationRequested)
        {
            completion.TrySetCanceled(this.token);
        }
        catch (Exception ex)
        {
            this.logger.Error(ex, "Server world operation failed; stopping session");
            completion.TrySetException(ex);
            // Never await the shutdown that joins this operation's RPC caller.
            this.session.Dispose();
        }
    }

    internal void OnMainLevelLoaded(Game game, LevelTransition transition, string levelId)
    {
        this.session.VerifyGameThread();
        if (this.token.IsCancellationRequested || this.activeOperation?.IsCompleted != false)
        {
            return;
        }


        this.loader.OnMainLevelLoaded(game, transition, levelId);
    }

    public async Task<byte[]> ReadSavedGameAsync(CancellationToken cancellationToken)
    {
        using var readCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.token, cancellationToken);
        await this.saveAvailable.Task.WaitAsync(readCancellation.Token).ConfigureAwait(false);
        this.ThrowIfStopping();
        return await File.ReadAllBytesAsync(this.savePath, readCancellation.Token).ConfigureAwait(false);
    }

    private void VerifyNoActiveOperation()
    {
        if (this.activeOperation?.IsCompleted == false)
        {

            throw new InvalidOperationException("Another server world operation is still running.");
        }

    }

    private void ThrowIfStopping() => this.token.ThrowIfCancellationRequested();

    // Session joins world producers before it finishes draining connections.
    internal async Task CompleteOperationsAsync()
    {
        await this.session.GameContext;
        this.session.VerifyGameThread();
        var initialLoad = this.initialLoadTask;
        var transition = this.transitionTask;
        if (initialLoad != null)
        {
            await this.ObserveAsync(initialLoad).ConfigureAwait(false);
        }

        if (transition != null)
        {
            await this.ObserveAsync(transition).ConfigureAwait(false);
        }

    }

    public void Dispose() => _ = this.StopAsync();
    public ValueTask DisposeAsync() => new(this.StopAsync());

    private Task StopAsync()
    {
        TaskCompletionSource completion;
        lock (this.gate)
        {
            if (this.stopped != null)
            {

                return this.stopped.Task;
            }

            completion = this.stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        this.lifetime.Cancel();
        this.saveAvailable.TrySetCanceled(this.token);
        this.session.Dispose();
        _ = this.FinishStopAsync(completion);
        return completion.Task;
    }

    private async Task FinishStopAsync(TaskCompletionSource completion)
    {
        try
        {
            await this.CompleteOperationsAsync();
            // RPC dispatch and connection removal must finish before native world cleanup.
            await this.session.Completion;
            await this.session.GameContext;
            this.loader.ResetObservation();
            this.CurrentWorld?.Dispose();
            this.CurrentWorld = null;
        }
        catch (Exception ex) { this.logger.Warning(ex, "World coordinator shutdown failed"); }
        finally
        {
            this.lifetime.Dispose();
            completion.TrySetResult();
        }
    }

    private async Task ObserveAsync(Task operation)
    {
        try
        { await operation; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { this.logger.Debug(ex, "World operation ended during shutdown"); }
    }
}
