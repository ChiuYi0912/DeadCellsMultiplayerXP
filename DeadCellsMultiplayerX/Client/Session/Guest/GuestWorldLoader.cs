namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics;
using System.Text.Json;
using dc;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Client.World;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// Owns the client-side level load: launch from save, wait for the game,
/// remove locally spawned mobs and replace the replicated world.  It does not
/// own session RPC, hooks or upload state.
/// </summary>
internal sealed class GuestWorldLoader(
    CancellationToken disposeToken,
    Func<ClientWorld> replaceWorld,
    Action resetLocalHeroState,
    Action<byte[]> saveDataSink,
    GuestClient client,
    ILogger logger)
{
    private readonly CancellationToken disposeToken = disposeToken;
    private readonly Func<ClientWorld> replaceWorld = replaceWorld
            ?? throw new ArgumentNullException(nameof(replaceWorld));
    private readonly Action resetLocalHeroState = resetLocalHeroState
            ?? throw new ArgumentNullException(nameof(resetLocalHeroState));
    private readonly Action<byte[]> saveDataSink = saveDataSink
            ?? throw new ArgumentNullException(nameof(saveDataSink));
    private readonly GuestClient client = client
            ?? throw new ArgumentNullException(nameof(client));
    private readonly ILogger logger = logger
            ?? throw new ArgumentNullException(nameof(logger));

    private static readonly JsonSerializerOptions DebugJsonOptions = new()
    {
        WriteIndented = true,
    };

    public async Task EnterNewLevel(byte[] saveData)
    {
        this.saveDataSink(saveData);
        this.logger.Information("Entering new level...");

        Main.Class.ME.options.disableLoreRooms = true;
        Main.Class.ME.cleanUser();
        Main.Class.ME.launchGame(new LaunchMode.LoadSave(), null, null);

        while (true)
        {
            await Task.Delay(1);
            this.disposeToken.ThrowIfCancellationRequested();

            var game = Game.Class.ME;
            if (game?.curLevel == null || game.subLevels == null)
            {
                continue;
            }

            if (!Main.Class.ME.isLoading)
            {
                break;
            }
        }

        this.logger.Information("Clearing entities...");
        var loadedGame = Game.Class.ME;

        foreach (Level level in loadedGame.subLevels)
        {
            List<Entity> entities = [];
            foreach (Entity entity in level.entities)
            {
                if (entity is Hero or Interactive)
                {
                    continue;
                }

                if (entity is Mob)
                {
                    entities.Add(entity);
                }
            }

            foreach (var entity in entities)
            {
                entity.destroy();
            }
        }

#if DEBUG
        await this.client.RefreshLobbyInfo();
        Debug.Assert(this.client.LobbyInfo != null);
        this.logger.Information(
            "Start New Game Team : {Info}\n",
            JsonSerializer.Serialize(this.client.LobbyInfo, DebugJsonOptions));
#endif

        this.replaceWorld();
        this.resetLocalHeroState();
    }
}
