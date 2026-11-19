namespace DeadCellsMultiplayerX;

#if DEBUG

using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Engine.Utilities;
using DeadCellsMultiplayerX.Server.Core;
using IngameDebugConsole;
using ModCore.Utilities;
using Serilog;
using dc.en.mob;

internal static class DebugConsoleCommands
{
    private static async void HoldAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "");
        }
    }

    [ConsoleMethod("dcmp-start-host", "启动 Host")]
    public static void StartHost(TextWriter writer, string ip = "127.0.0.1", int port = 44567)
    {
        HoldAsync(ClientRuntime.Instance.StartHost(ip, port));
        writer.Write("done...");
    }

    [ConsoleMethod("dcmp-start-guest", "启动 Guest")]
    public static void ConnectLobby(TextWriter writer, string ip, int port)
    {
        HoldAsync(ClientRuntime.Instance.StartGuest(ip, port));
        writer.Write("done...");
    }

    [ConsoleMethod("dcmp-quit-lobby", "离开房间")]
    public static void QuitLobby(TextWriter writer)
    {
        ClientRuntime.Instance.CurrentGuestClient!.Quit();
        writer.Write("done...");
    }

    [ConsoleMethod("dcmp-set-name", "修改玩家名称")]
    public static void SetName(TextWriter writer, string name)
    {
        ClientRuntime.Instance.CurrentGuestClient!.SetName(name);
        writer.Write("done...");
    }


    [ConsoleMethod("dcmp-worldframe-drop-server-frame", "丢弃下一个服务端世界帧以测试客户端 NACK")]
    public static void DropServerFrame(TextWriter writer, int count = 1)
    {
        WorldFrameDebug.DropServerFrames += Math.Max(1, count);
        writer.WriteLine($"Will drop {WorldFrameDebug.DropServerFrames} server frame(s).");
    }

    [ConsoleMethod("dcmp-worldframe-skip-client-upload", "跳过下一次客户端上传以测试服务端 NACK")]
    public static void SkipClientUpload(TextWriter writer, int count = 1)
    {
        WorldFrameDebug.SkipClientUploads += Math.Max(1, count);
        writer.WriteLine($"Will skip {WorldFrameDebug.SkipClientUploads} client upload(s).");
    }

    [ConsoleMethod("dcmp-worldframe-force-resync", "强制客户端请求全量世界重同步")]
    public static void ForceResync(TextWriter writer)
    {
        WorldFrameDebug.ForceResync = true;
        writer.WriteLine("World resync will be requested on next tick.");
    }

    [ConsoleMethod("dcmp-next-level", "请求切换关卡")]
    public static void NextLevel(TextWriter writer, string levelid)
    {
        var guest = ClientRuntime.Instance.CurrentGuestClient;
        if (guest == null)
        {
            writer.WriteLine("No guest client.");
            return;
        }

        HoldAsync(guest.SubmitLevelVote(levelid));
        writer.WriteLine($"Requested next level: {levelid}");
    }

    [ConsoleMethod("dcmp-server-level", "服务端强制切换关卡并广播", "s-level")]
    public static void ServerBroadcastNextLevel(TextWriter writer, string levelid)
    {
        var session = ServerMain.Instance?.Session;
        if (session == null)
        {
            writer.WriteLine("Server session is not available in this process.");
            return;
        }

        HoldAsync(session.LevelTransitions
            .ForceLevelAsync(levelid, CancellationToken.None)
            .AsTask());
        writer.WriteLine($"Server forced level: {levelid}");
    }


    [ConsoleMethod("dcmp-hero-journal", "打印服务端动作处理日志")]
    public static void HeroJournal(TextWriter writer, int count = 32)
    {
        var world = ServerMain.Instance?.Worlds?.CurrentWorld;
        if (world == null)
        {
            writer.WriteLine("Server world is not available in this process.");
            return;
        }

        world.Ghosts.Journal.Dump(writer, count);
    }

    [ConsoleMethod("dcmp-worldframe-stats", "打印客户端世界帧统计")]
    public static void WorldFrameStats(TextWriter writer)
    {
        var receiver = WorldFrameDebug.Receiver;
        if (receiver == null)
        {
            writer.WriteLine("WorldFrameReceiver not available.");
            return;
        }

        writer.WriteLine($"epoch={receiver.Epoch} baseline={receiver.HasBaseline} " +
                         $"lastSeq={receiver.LastDeliveredSequence} pending={receiver.PendingCount}");
        writer.WriteLine($"gap={receiver.GapCount} nack={receiver.NackSent} " +
                         $"bundle={receiver.BundleReceived} skipped={receiver.SkippedFrames} " +
                         $"resync={receiver.ResyncCount} droppedOld={receiver.DroppedOld} " +
                         $"overflow={receiver.PendingOverflow}");
    }

    [ConsoleMethod("mob", "")]
    public static void CreateMobToServer(TextWriter writer)
    {
        var game = dc.pr.Game.Class.ME;
        var self = game.hero;
        //Mob.Class.create("Scarecrow".AsHaxeString(), game.curLevel, self.cx, self.cy, 30, Ref<int>.In(30));

        var mob = new dc.level.Mob("zombie".AsHaxeString(), self.cx, self.cy, 30, 30, false);
        game.curLevel.attachMob(mob);
        writer.Write("done...");

        // var testItem = new InventItem(
        //     new InventItemKind.Weapon("Burner".AsHaxeString())
        // );
        // var itemDrop = new ItemDrop(
        //     self._level, self.cx, self.cy,
        //     testItem, true,
        //     Ref<bool>.In(false)
        // );
        // itemDrop.init();
        // itemDrop.onDropAsLoot();
        // itemDrop.dx = self.dx;
    }

    [ConsoleMethod("erro-ui", "")]
    public static void ShowErroUI(TextWriter writer)
    {
        try
        {
            var mob = new Zombie(null, 2, 2, 22, 2);
            mob.init();
        }
        catch (Exception ex)
        {
            Dialog.ReportError(ex.ToString());
            throw;
        }
        writer.Write("done...");
    }
}
#endif
