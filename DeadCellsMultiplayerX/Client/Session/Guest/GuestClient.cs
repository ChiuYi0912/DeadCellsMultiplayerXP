using dc.tool;
using DeadCellsMultiplayerX.Networking.Transport;
using StreamJsonRpc;
using System.Diagnostics;
using DeadCellsMultiplayerX.Contracts.Lobby;


using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Versioning;
namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    internal class GuestClient(BaseNetworkConnection remote) : ClientBase
    {
        private JsonRpc? rpc;
        private IHostClientRPC? hostInterfact;
        private GuestClientSession? session;

        public LobbyInfo? LobbyInfo { get; set; }

        public string Guid { get; set; } = "";

        public bool IsServerDrivenTransition => session != null ? session.serverDrivenTransition : false;

        internal GuestClientSession? Session => session;

        public async Task Init(string name)
        {

            rpc = remote.Stream.CreateJsonRpc("guest-lobby", NetLogSession.CreateJsonRpcFailureListener("guest-lobby"));

            hostInterfact = rpc.Attach<IHostClientRPC>();

            rpc.Disconnected += Rpc_Disconnected;
            rpc.StartListening();

            if (!await hostInterfact.CheckVersion(
                VersionUtils.ModVersion.ToString()
                ))
            {
                Logger.Information("Failed to connect lobby. Dismatch version.");
                Dispose();
                return;
            }

            Guid = await hostInterfact.GetGUID();

            SetName(name);
            await SetSkinMould(Save.Class.tryLoad().heroSkin.ToString());
            SetReady(false);

            LobbyInfo = await hostInterfact.GetLobbyInfo();
            _ = MessageLoop();
        }

        private async Task MessageLoop()
        {
            Debug.Assert(hostInterfact != null);
            await Task.Delay(1).ConfigureAwait(false);

            while (!IsDisposed && session == null)
            {
                DisposeToken.ThrowIfCancellationRequested();

                LobbyInfo = await hostInterfact.GetLobbyInfo();

                if (LobbyInfo.CanConnectServer)
                {
                    session = new GuestClientSession(this, await hostInterfact.GetServerStream());
                    await session.Init();
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(0.5));
            }
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
            base.MyDispose();
            DisposeSafely(session, "Guest server session");
            DisposeSafely(rpc, "Guest lobby RPC");
            DisposeSafely(remote, "Guest QUIC connection");
        }

        protected override async ValueTask MyDisposeAsync()
        {
            base.MyDispose();

            if (session != null)
            {
                await DisposeSafelyAsync(
                    () => session.DisposeAsync(),
                    "Guest server session").ConfigureAwait(false);
            }

            DisposeSafely(rpc, "Guest lobby RPC");
            await DisposeSafelyAsync(
                () => remote.DisposeAsync(),
                "Guest QUIC connection").ConfigureAwait(false);
        }

        public void SetName(string name)
        {
            Debug.Assert(hostInterfact != null);

            hostInterfact.SetName(name);
        }

        public void SetReady(bool ready)
        {
            Debug.Assert(hostInterfact != null);

            hostInterfact.SetReady(ready);
        }

        public Task SetSkinMould(string skinMould)
        {
            Debug.Assert(hostInterfact != null);
            return hostInterfact.SetSkinMould(skinMould);
        }


        public async Task<long> Ping()
        {
            if (IsDisposed || rpc == null || rpc.IsDisposed)
                return -1;

            Debug.Assert(hostInterfact != null);
            var sw = Stopwatch.StartNew();
            await hostInterfact.Ping();
            return sw.ElapsedMilliseconds;
        }

        public async Task RefreshLobbyInfo()
        {
            Debug.Assert(hostInterfact != null);
            LobbyInfo = await hostInterfact.GetLobbyInfo();
        }

        public async Task SelectNextLevel(string levelid)
        {
            Debug.Assert(session != null);
            await session.Server.GusetEnterNextLevel(session.Client.Guid, levelid);
        }

        public void Quit()
        {
            Debug.Assert(hostInterfact != null);

            if (!(rpc?.IsDisposed ?? true))
            {
                hostInterfact.Quit();
            }

            Dispose();
        }

    }
}
