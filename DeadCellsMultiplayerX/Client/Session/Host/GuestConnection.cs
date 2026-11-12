using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Networking.Transport;
using ModCore.Events;
using ModCore.Modules;
using Nerdbank.Streams;
using Serilog;
using StreamJsonRpc;
using System.Diagnostics;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Versioning;
namespace DeadCellsMultiplayerX.Client.Session.Host
{
    internal class GuestConnection : Disposable, IHostClientRPC, IDisposable
    {
        private readonly HostClient host;
        private readonly BaseNetworkConnection connection;

        private JsonRpc? rpc;
        public GuestInfo guestInfo = new();
        public override ILogger Logger { get; }

        public GuestConnection(HostClient host, BaseNetworkConnection connection)
        {
            this.host = host;
            this.connection = connection;

            Logger = Log.ForContext("SourceContext", "Guest-" + guestInfo.Guid);
        }

        protected override void MyDispose()
        {
            EventSystem.BroadcastEvent<IOnGuestQuit, GuestInfo>(guestInfo);

            host.LobbyInfo.Guests.Remove(guestInfo.Guid);
            DisposeSafely(rpc, "Guest lobby RPC");
            DisposeSafely(channel, "Guest channel");
            channel = null;
            DisposeSafely(connection, "Guest QUIC connection");
        }

        protected override async ValueTask MyDisposeAsync()
        {
            EventSystem.BroadcastEvent<IOnGuestQuit, GuestInfo>(guestInfo);

            host.LobbyInfo.Guests.Remove(guestInfo.Guid);
            DisposeSafely(rpc, "Guest lobby RPC");
            DisposeSafely(channel, "Guest channel");
            channel = null;
            await DisposeSafelyAsync(
                () => connection.DisposeAsync(),
                "Guest QUIC connection").ConfigureAwait(false);
        }

        public void Init()
        {

            host.LobbyInfo.Guests.Add(guestInfo.Guid, guestInfo);

            rpc = connection.Stream.CreateJsonRpc("host-guest", NetLogSession.CreateJsonRpcFailureListener("host-guest"));

            rpc.AddLocalRpcTarget(this);
            rpc.Disconnected += Rpc_Disconnected;
            rpc.SynchronizationContext = Game.SynchronizationContext;
            rpc.StartListening();
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

        // IHostClientRPC
        public Task<string> GetGUID()
        {
            return Task.FromResult(guestInfo.Guid);
        }

        public Task<LobbyInfo> GetLobbyInfo()
        {
            return Task.FromResult(host.LobbyInfo);
        }


        public void SetName(string name)
        {
            Logger.Information("Set name as '{name}'", guestInfo.Guid, name);
            guestInfo.Name = name;
        }

        public Task SetSkinMould(string skinMould)
        {
            guestInfo.SkinMould = skinMould;
            return Task.CompletedTask;
        }

        public void Quit()
        {
            Logger.Information("Quit.", guestInfo.Guid);

            Dispose();
        }

        public Task<bool> CheckVersion(string version)
        {
            if (Version.Parse(version) != VersionUtils.ModVersion)
            {
                Logger.Error("({guid}) Dismatch version: {ver}", guestInfo.Guid, version);
                Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
                {
                    if (rpc == null)
                    {
                        return;
                    }
                    if (!rpc.IsDisposed)
                    {
                        Dispose();
                    }
                });
                return Task.FromResult(false);
            }
            return Task.FromResult(true);
        }

        public void SetReady(bool ready)
        {
            Logger.Information("Set Ready: {val}", guestInfo.Guid, ready);
            guestInfo.IsReady = ready;
        }

        public Task Ping() => Task.CompletedTask;


        public Task<Stream> GetServerStream()
        {
            if (!host.LobbyInfo.CanConnectServer || host.session == null)
                throw new InvalidOperationException();

            Logger.Information("Connected server");
            channel = host.session.AllocNewChannel();
            var stream = channel.AsStream();

            //写入客户guid
            var guidBytes = System.Text.Encoding.UTF8.GetBytes(guestInfo.Guid);
            Debug.Assert(guidBytes.Length == 36);
            stream.Write(guidBytes, 0, guidBytes.Length);
            stream.Flush();

            Debug.Assert(IsConnectedServer);
            return Task.FromResult(stream);
        }

        //

        MultiplexingStream.Channel? channel;

        /// <summary>
        /// 是否连接到服务器
        /// </summary>
        public bool IsConnectedServer => channel != null;

        public void StartConnectServer()
        {
            Task.Delay(TimeSpan.FromSeconds(
                ModConfig.Config.Value.StartTimeout
                ), DisposeToken
                ).ContinueWith(_ =>
                {
                    if (!IsConnectedServer)
                    {
                        Logger.Error("Failed to connect server. Timeout.");
                        Dispose();
                    }
                });
        }

    }
}
