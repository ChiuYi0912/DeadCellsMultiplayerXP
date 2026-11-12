using dc.en;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Server.Integration.Events;
using ModCore.Events;
using ModCore.Modules;
using Serilog;
using StreamJsonRpc;
using System.Diagnostics;

using DeadCellsMultiplayerX.Contracts.Lobby;

using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Diagnostics;
namespace DeadCellsMultiplayerX.Server.Session.Connection
{
    internal partial class ServerGuestConnection :
        DisposableEventReceiver,
        IOnServerEnterNewLevel
    {


        private readonly JsonRpc rpc;
        public GuestInfo guestInfo = new();
        public override ILogger Logger { get; }
        public GuestInfo GuestInfo { get; set; } = new();
        public ServerSession Session { get; }
        public ServerMainThread Main => Session.Main;
        public IGuestRPC guest;

        public ServerGuestConnection(ServerSession session, Stream connection)
        {
            Session = session;
            Logger = Log.ForContext("SourceContext", "Server-Guest-" + guestInfo.Guid);

            rpc = connection.CreateJsonRpc("server-guest", NetLogSession.CreateJsonRpcFailureListener("server-guest"));

            rpc.AddLocalRpcTarget(this);


            guest = rpc.Attach<IGuestRPC>();

            rpc.SynchronizationContext = Game.SynchronizationContext;

            rpc.Disconnected += Rpc_Disconnected;

            rpc.StartListening();

            Logger.Information("Connected.");
        }

        private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            if (e.Reason == DisconnectedReason.LocallyDisposed)
            {
                return;
            }
            Logger.Error(e.Exception, "Abort connection: {reason}: {desc}", e.Reason, e.Description);
            Dispose();
        }

        protected override void MyDispose()
        {
            Session.RemoveGuest(this);

            base.MyDispose();

            rpc?.Dispose();
        }

        /// <summary>广播一帧世界快照(不阻塞游戏线程)。</summary>
        public void PushWorldFrame(WorldFrame frame)
        {
            if (IsDisposed || rpc.IsDisposed) return;

            try
            {
                var task = guest.SyncWorldFrame(frame);
                _ = task.ContinueWith(
                    t => Logger.Warning(t.Exception, "SyncWorldFrame failed"),
                    TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                // 同步序列化/连接异常不能冒泡到广播循环,否则会打断整个服务端 tick
                Logger.Warning(ex, "SyncWorldFrame failed (sync)");
            }
        }

        /// <summary>向该客户端请求补发上传帧。</summary>
        public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
            => guest.RequestClientFrames(request);

        void IOnServerEnterNewLevel.OnServerEnterNewLevel()
        {
            Debug.Assert(Main.savePath != null);

            guest.EnterNewLevel(File.ReadAllBytes(Main.savePath));

            EventSystem.BroadcastEvent<IOnHeroInitDone, Hero>(Game.Instance.HeroInstance!);
        }



    }
}
