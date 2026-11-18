namespace DeadCellsMultiplayerX.Server.Session;

using dc;
using dc.cine;
using dc.pr;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.Integration.Events;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Utilities;

/// <summary>
/// 这个类的所有 Method 应该在主线程执行
/// </summary>
internal sealed class ServerMainThread(SynchronizationContext gameContext, Thread gameThread) : DisposableEventReceiver
{
    public string? SavePath { get; private set; }

    private void CheckMainThread()
    {
        if (Thread.CurrentThread != gameThread)
        {
            throw new InvalidOperationException("Native server game operations must run on the game thread.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public async Task LaunchGame()
    {
        await gameContext;
        this.CheckMainThread();
        this.DisposeToken.ThrowIfCancellationRequested();

        this.SavePath = null;

        this.Logger.Information("Launching game...");

        Main.Class.ME.launchGame(new LaunchMode.NewGame(false, false), null, null);

        while (true)
        {
            await Task.Delay(1, this.DisposeToken);
            await gameContext;
            this.DisposeToken.ThrowIfCancellationRequested();
            this.CheckMainThread();
            var g = Game.Class.ME;

            if (g == null ||
                g.curLevel == null ||
                g.subLevels == null ||
                g.hero == null ||
                Main.Class.ME.isLoading)
            {
                continue;
            }
            break;
        }

        await this.EnterNewLevel();
    }

    /// <summary>
    /// Moves the server game to the next level selected by the guests.
    /// This deliberately does not write a save or broadcast
    /// <see cref="IOnServerEnterNewLevel"/>: the initial-load path uses the
    /// save-file transfer, while the next-level path lets each client run its
    /// own <c>LevelTransition.goto</c> via <c>IGuestRPC.EnterNextLevel</c>.
    /// </summary>
    public async Task EnterNextLevel(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
        {
            throw new ArgumentException("Level id is required.", nameof(levelId));
        }

        await gameContext;
        this.CheckMainThread();
        this.DisposeToken.ThrowIfCancellationRequested();

        this.Logger.Information("Server entering next level: {LevelId}", levelId);
        LevelTransition.Class.@goto.Invoke(levelId.AsHaxeString());

        while (true)
        {
            await Task.Delay(1, this.DisposeToken);
            await gameContext;
            this.DisposeToken.ThrowIfCancellationRequested();
            this.CheckMainThread();

            var game = Game.Class.ME;
            if (game == null ||
                game.curLevel == null ||
                game.subLevels == null ||
                game.hero == null ||
                Main.Class.ME.isLoading)
            {
                continue;
            }

            break;
        }

        this.Logger.Information("Server entered next level: {LevelId}", levelId);
    }

    public async Task EnterNewLevel()
    {
        await gameContext;
        this.CheckMainThread();
        this.DisposeToken.ThrowIfCancellationRequested();
        this.Logger.Information("Entering new level...");

        this.Logger.Information("Saving game...");

        Main.Class.ME.writeSave();

        while (Main.Class.ME.waitToSave)
        {
            this.DisposeToken.ThrowIfCancellationRequested();
            await Task.Delay(1, this.DisposeToken);
            await gameContext;
            this.CheckMainThread();
            this.DisposeToken.ThrowIfCancellationRequested();
            Main.Class.ME.writeSave();
        }

        this.Logger.Information("Saved game");

        this.SavePath = ServerMain.Instance.SavePath;
        EventSystem.BroadcastEvent<IOnServerEnterNewLevel>();
    }
}
