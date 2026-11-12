namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics;
using dc.tool;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using Serilog;
using StreamJsonRpc;

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
        ArgumentNullException.ThrowIfNull(localTarget);
        ArgumentNullException.ThrowIfNull(serverStream);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(onDisconnected);
        ArgumentNullException.ThrowIfNull(logger);

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
            MAJOR = ProtocolVersion.Major,
            MINOR = ProtocolVersion.Minor,
            ModVersion = VersionUtils.ModVersion.ToString(),
            Capabilities = ProtocolVersion.SupportedCapabilities,
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

        var isOwner = lobby.Owner == client.Guid;
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
