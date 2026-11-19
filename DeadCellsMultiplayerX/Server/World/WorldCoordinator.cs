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
    private TaskCompletionSource worldReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private string? targetLevelId;

    public ServerWorld? CurrentWorld { get; private set; }
    public int Epoch { get; private set; }

    public WorldCoordinator(ServerSession session, string savePath)
    {
        session.VerifyGameThread();
        this.session = session;
        this.savePath = savePath;
        this.loader = new(session.GameContext, Thread.CurrentThread, savePath);
        this.lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.DisposeToken);
        this.token = this.lifetime.Token;
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
        this.targetLevelId = levelId;
        this.CurrentWorld?.Stop();
        this.worldReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
        // Once accepted, only the coordinator lifetime cancels native work, not its caller's wait.
        _ = this.CompleteOperationAsync(this.EnterNextLevelCoreAsync(levelId), completion);
        return new(completion.Task);
    }

    private async Task LoadInitialGameCoreAsync(byte[] data)
    {
        var savedGame = await this.loader.LoadSavedGameAsync(data, this.token).ConfigureAwait(false);
        await this.session.GameContext;
        this.ThrowIfStopping();
        this.StartWorld();
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
        this.StartWorld();
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

    internal void OnLevelLoading(LevelTransition transition)
    {
        this.session.VerifyGameThread();
        if (this.token.IsCancellationRequested || this.activeOperation?.IsCompleted != false) return;
        // Sub-level activation keeps the current world; extend this branch when synchronized.
        if (transition.mainId == null && transition.heroPosAfterBossRuneReload == null) return;
        var levelId = transition.mainId?.ToString()
            ?? Game.Class.ME.curLevel.map.id.ToString();
        if (this.targetLevelId != null && this.targetLevelId != levelId)
            throw new InvalidOperationException("Native level does not match the requested target.");
        this.ReleaseWorld();
        this.ThrowIfStopping();
        this.CurrentWorld = new(() => this.session.CurrentTimeStamp, ++this.Epoch);
        foreach (var guest in this.session.ConnectedGuests)
            this.CurrentWorld.AddGuest(guest.ConnectionGuid, guest.RequestClientFrames);
        this.loader.OnLevelLoading(transition, levelId);
    }

    private void StartWorld()
    {
        var world = this.CurrentWorld ?? throw new InvalidOperationException("Native loading did not create a world.");
        world.Start();
        this.targetLevelId = null;
        this.worldReady.TrySetResult();
    }

    public async Task<ServerWorld> WaitForWorldAsync(CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(this.token, cancellationToken);
        while (true)
        {
            await this.session.GameContext;
            wait.Token.ThrowIfCancellationRequested();
            if (this.CurrentWorld is { IsRunning: true } world) return world;
            await this.worldReady.Task.WaitAsync(wait.Token).ConfigureAwait(false);
        }
    }

    private void ReleaseWorld()
    {
        // Keep lookup alive for reentrant native cleanup; detach before native level disposal.
        this.CurrentWorld?.Dispose();
        this.CurrentWorld = null;
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
            this.loader.Dispose();
            this.ReleaseWorld();
            this.targetLevelId = null;
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
