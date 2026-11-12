using dc;
using dc.cine;
using dc.pr;
using DeadCellsMultiplayerX.Server.Integration.Events;
using ModCore.Events;
using ModCore.Utilities;
using DeadCellsMultiplayerX.Server.Core;

using DeadCellsMultiplayerX.Foundation.Lifecycle;
namespace DeadCellsMultiplayerX.Server.Session
{
    /// <summary>
    /// 这个类的所有 Method 应该在主线程执行
    /// </summary>
    internal class ServerMainThread : DisposableEventReceiver
    {

        public string? savePath;

        private static void CheckMainThread()
        {
            if (Thread.CurrentThread != ServerMain.Instance.MainThread)
            {
                throw new InvalidOperationException();
            }
        }
        private static Task RunOnMainThread()
        {
            SynchronizationContext.SetSynchronizationContext(ModCore.Modules.Game.SynchronizationContext);
            return Task.Delay(1);
        }

        /// <summary>
        /// 
        /// </summary>
        public async Task LaunchGame()
        {
            await RunOnMainThread();

            CheckMainThread();

            savePath = null;

            Logger.Information("Launching game...");

            Main.Class.ME.launchGame(new LaunchMode.NewGame(false, false), null, null);

            while (true)
            {
                await Task.Delay(1);
                DisposeToken.ThrowIfCancellationRequested();
                CheckMainThread();
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

            await EnterNewLevel();
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
                throw new ArgumentException("Level id is required.", nameof(levelId));

            await RunOnMainThread();
            CheckMainThread();

            Logger.Information("Server entering next level: {LevelId}", levelId);
            LevelTransition.Class.@goto.Invoke(levelId.AsHaxeString());

            while (true)
            {
                await Task.Delay(1);
                DisposeToken.ThrowIfCancellationRequested();
                CheckMainThread();

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

            Logger.Information("Server entered next level: {LevelId}", levelId);
        }

        public async Task EnterNewLevel()
        {
            Logger.Information("Entering new level...");

            Logger.Information("Saving game...");

            Main.Class.ME.writeSave();

            while (Main.Class.ME.waitToSave)
            {
                DisposeToken.ThrowIfCancellationRequested();
                await Task.Delay(1);
                Main.Class.ME.writeSave();
            }

            Logger.Information("Saved game");

            savePath = ServerMain.Instance.savePath;
            EventSystem.BroadcastEvent<IOnServerEnterNewLevel>();
        }
    }
}
