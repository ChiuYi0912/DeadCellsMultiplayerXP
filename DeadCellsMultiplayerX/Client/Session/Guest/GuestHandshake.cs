using System.Diagnostics;
using dc.tool;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using Serilog;
using StreamJsonRpc;

namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    /// <summary>
    /// Creates the guest-server RPC, performs the protocol/version handshake and
    /// uploads the owner's save.  On failure it disposes the RPC and returns null;
    /// the session remains responsible for its own disposal.
    /// </summary>
    internal static class GuestHandshake
    {
        internal sealed record Result(
            JsonRpc Rpc,
            IServerRPC Server,
            bool IsOwner);

        public static async Task<Result?> RunAsync(
            IGuestRPC localTarget,
            Stream serverStream,
            GuestClient client,
            EventHandler<JsonRpcDisconnectedEventArgs> onDisconnected,
            ILogger logger)
        {
            if (localTarget == null)
                throw new ArgumentNullException(nameof(localTarget));
            if (serverStream == null)
                throw new ArgumentNullException(nameof(serverStream));
            if (client == null)
                throw new ArgumentNullException(nameof(client));
            if (onDisconnected == null)
                throw new ArgumentNullException(nameof(onDisconnected));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var rpc = serverStream.CreateJsonRpc(
                "guest-server",
                NetLogSession.CreateJsonRpcFailureListener("guest-server"));
            rpc.AddLocalRpcTarget(localTarget);

            var server = rpc.Attach<IServerRPC>();
            rpc.SynchronizationContext =
                ModCore.Modules.Game.SynchronizationContext;
            rpc.Disconnected += onDisconnected;
            rpc.StartListening();

            var hello = await server.Hello(new HelloRequest
            {
                MAJOR = ProtocolVersion.MAJOR,
                MINOR = ProtocolVersion.MINOR,
                ModVersion = VersionUtils.ModVersion.ToString(),
                Capabilities = ProtocolVersion.SUPPORTED_CAPABILITIES,
            });

            if (!hello.Accepted)
            {
                logger.Error(
                    "Protocol handshake rejected: {Reason}", hello.Reason);
                rpc.Dispose();
                return null;
            }

            if (!await server.CheckVersion(
                    VersionUtils.ModVersion.ToString()))
            {
                logger.Information(
                    "Failed to connect lobby. Dismatch version.");
                rpc.Dispose();
                return null;
            }

            var lobby = client.LobbyInfo;
            if (lobby == null)
            {
                logger.Error("Lobby info is missing after handshake.");
                rpc.Dispose();
                return null;
            }

            bool isOwner = lobby.Owner == client.Guid;
            logger.Information("Updating guest info...");
            await server.SetGuestInfo(lobby.Guests[client.Guid]);

            if (isOwner)
            {
                logger.Information("Updating savedata...");
                var path = System.IO.Path.GetFullPath(
                    "save/" + Save.Class.fileName(null).ToString());

                // 必须使用现有存档进行联机
                Debug.Assert(System.IO.File.Exists(path));
                await server.UploadSavedata(await System.IO.File.ReadAllBytesAsync(path));
            }

            return new Result(rpc, server, isOwner);
        }
    }
}
