using System.Diagnostics;
using System.Text.Json;
using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.World;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    /// <summary>
    /// Owns the client-side level load: launch from save, wait for the game,
    /// remove locally spawned mobs and replace the replicated world.  It does not
    /// own session RPC, hooks or upload state.
    /// </summary>
    internal sealed class GuestWorldLoader
    {
        private readonly CancellationToken disposeToken;
        private readonly Func<ClientWorld> replaceWorld;
        private readonly Action resetLocalHeroState;
        private readonly Action<byte[]> saveDataSink;
        private readonly GuestClient client;
        private readonly ILogger logger;

        public GuestWorldLoader(
            CancellationToken disposeToken,
            Func<ClientWorld> replaceWorld,
            Action resetLocalHeroState,
            Action<byte[]> saveDataSink,
            GuestClient client,
            ILogger logger)
        {
            this.disposeToken = disposeToken;
            this.replaceWorld = replaceWorld
                ?? throw new ArgumentNullException(nameof(replaceWorld));
            this.resetLocalHeroState = resetLocalHeroState
                ?? throw new ArgumentNullException(nameof(resetLocalHeroState));
            this.saveDataSink = saveDataSink
                ?? throw new ArgumentNullException(nameof(saveDataSink));
            this.client = client
                ?? throw new ArgumentNullException(nameof(client));
            this.logger = logger
                ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task EnterNewLevel(byte[] saveData)
        {
            saveDataSink(saveData);
            logger.Information("Entering new level...");

            Main.Class.ME.options.disableLoreRooms = true;
            Main.Class.ME.cleanUser();
            Main.Class.ME.launchGame(new LaunchMode.LoadSave(), null, null);

            while (true)
            {
                await Task.Delay(1);
                disposeToken.ThrowIfCancellationRequested();

                var game = Game.Class.ME;
                if (game?.curLevel == null || game.subLevels == null)
                    continue;

                if (!Main.Class.ME.isLoading)
                    break;
            }

            logger.Information("Clearing entities...");
            var loadedGame = Game.Class.ME;

            foreach (Level level in loadedGame.subLevels)
            {
                List<Entity> entities = [];
                foreach (Entity entity in level.entities)
                {
                    if (entity is Hero or Interactive)
                        continue;

                    if (entity is Mob)
                        entities.Add(entity);
                }

                foreach (var entity in entities)
                    entity.destroy();
            }

#if DEBUG
            await client.RefreshLobbyInfo();
            Debug.Assert(client.LobbyInfo != null);
            var options = new JsonSerializerOptions { WriteIndented = true };
            logger.Information(
                "Start New Game Team : {Info}\n",
                JsonSerializer.Serialize(client.LobbyInfo, options));
#endif

            replaceWorld();
            resetLocalHeroState();
        }
    }
}
