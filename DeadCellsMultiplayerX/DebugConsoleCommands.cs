using dc.en.inter;
using dc.tool;
using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Tools;
using HaxeProxy.Runtime;
using IngameDebugConsole;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX
{
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
        }

        [ConsoleMethod("dcmp-start-guest", "启动 Guest")]
        public static void ConnectLobby(TextWriter writer, string ip, int port)
        {
            HoldAsync(ClientRuntime.Instance.StartGuest(ip, port));
        }

        [ConsoleMethod("dcmp-quit-lobby", "离开房间")]
        public static void QuitLobby(TextWriter writer)
        {
            ClientRuntime.Instance.CurrentGuestClient!.Quit();
        }

        [ConsoleMethod("dcmp-set-name", "修改玩家名称")]
        public static void SetName(TextWriter writer, string name)
        {
            ClientRuntime.Instance.CurrentGuestClient!.SetName(name);
        }

#if DEBUG
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

        [ConsoleMethod("dcmp-next-level", "请求切换关卡（Guest，用于换关 smoke）")]
        public static void NextLevel(TextWriter writer, string levelid)
        {
            var guest = ClientRuntime.Instance.CurrentGuestClient;
            if (guest == null)
            {
                writer.WriteLine("No guest client.");
                return;
            }

            HoldAsync(guest.SelectNextLevel(levelid));
            writer.WriteLine($"Requested next level: {levelid}");
        }

        [ConsoleMethod("dcmp-protocol-baseline", "生成协议基线（完整 serializer + RPC 回环）")]
        public static void RunProtocolBaseline(TextWriter writer, string path = "")
        {
            if (string.IsNullOrWhiteSpace(path))
                path = System.IO.Path.Combine(
                    Environment.CurrentDirectory, "protocol-baseline-full.json");

            ProtocolBaseline.Run(path);
            writer.WriteLine($"Protocol baseline written: {path}");
        }

        [ConsoleMethod("dcmp-worldframe-stats", "打印客户端世界帧统计")]
        public static void WorldFrameStats(TextWriter writer)
        {
            var manager = WorldFrameDebug.ClientManager;
            if (manager == null)
            {
                writer.WriteLine("WorldFrameTimeline not available.");
                return;
            }

            writer.WriteLine($"epoch={manager.Epoch} baseline={manager.HasBaseline} " +
                             $"lastSeq={manager.LastDeliveredSequence} pending={manager.PendingCount}");
            writer.WriteLine($"gap={manager.GapCount} nack={manager.NackSent} " +
                             $"bundle={manager.BundleReceived} skipped={manager.SkippedFrames} " +
                             $"resync={manager.ResyncCount} droppedOld={manager.DroppedOld} " +
                             $"overflow={manager.PendingOverflow}");
        }

        [ConsoleMethod("mob", "")]
        public static void TestMob(TextWriter writer)
        {
            dc.pr.Game game = dc.pr.Game.Class.ME;
            var self = game.hero;
            //Mob.Class.create("Scarecrow".AsHaxeString(), game.curLevel, self.cx, self.cy, 30, Ref<int>.In(30));

            var mob = new dc.level.Mob("zombie".AsHaxeString(), self.cx, self.cy, 30, 30, false);
            game.curLevel.attachMob(mob);

            InventItem testItem = new InventItem(
                new InventItemKind.Weapon("Burner".AsHaxeString())
            );
            ItemDrop itemDrop = new ItemDrop(
                self._level, self.cx, self.cy,
                testItem, true,
                Ref<bool>.In(false)
            );
            itemDrop.init();
            itemDrop.onDropAsLoot();
            itemDrop.dx = self.dx;
        }
#endif
    }
}
