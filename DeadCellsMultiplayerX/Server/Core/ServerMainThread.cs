using dc;
using dc.pr;
using DeadCellsMultiplayerX.Common.Core;
using DeadCellsMultiplayerX.Server.Events;
using ModCore.Events;
using ModCore.Storage;
using ModCore.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.Core
{
    /// <summary>
    /// 这个类的所有 Method 应该在主线程执行
    /// </summary>
    internal class ServerMainThread(ServerSession session) : DisposableEventReceiver
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

        public async Task EnterNewLevel()
        {
            Logger.Information("Entering new level...");

            Logger.Information("Saving game...");

            Main.Class.ME.writeSave();

            while (Main.Class.ME.waitToSave)
            {
                await Task.Delay(1);
                Main.Class.ME.writeSave();
            }

            Logger.Information("Saved game");

            savePath = ServerMain.Instance.savePath;
            EventSystem.BroadcastEvent<IOnServerEnterNewLevel>();
        }
    }
}
