namespace DeadCellsMultiplayerX.Client.Session.Host;

using System.Diagnostics;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using ModCore.Events;
using ModCore.Modules;
using Nerdbank.Streams;
using Serilog;
using StreamJsonRpc;

internal sealed class GuestConnection : Disposable, IHostClientRPC, IDisposable
{
    private readonly HostClient host;
    private readonly BaseNetworkConnection connection;

    private JsonRpc? rpc;
    public GuestInfo GuestInfo { get; set; } = new();
    public override ILogger Logger { get; }

    public GuestConnection(HostClient host, BaseNetworkConnection connection)
    {
        this.host = host;
        this.connection = connection;

        this.Logger = Log.ForContext("SourceContext", "Guest-" + this.GuestInfo.Guid);
    }

    protected override void MyDispose()
    {
        EventSystem.BroadcastEvent<IOnGuestQuit, GuestInfo>(this.GuestInfo);

        this.host.LobbyInfo.Guests.Remove(this.GuestInfo.Guid);
        this.DisposeSafely(this.rpc, "Guest lobby RPC");
        this.DisposeSafely(this.channel, "Guest channel");
        this.channel = null;
        this.DisposeSafely(this.connection, "Guest QUIC connection");
    }

    protected override async ValueTask MyDisposeAsync()
    {
        EventSystem.BroadcastEvent<IOnGuestQuit, GuestInfo>(this.GuestInfo);

        this.host.LobbyInfo.Guests.Remove(this.GuestInfo.Guid);
        this.DisposeSafely(this.rpc, "Guest lobby RPC");
        this.DisposeSafely(this.channel, "Guest channel");
        this.channel = null;
        await this.DisposeSafelyAsync(
            this.connection.DisposeAsync,
            "Guest QUIC connection").ConfigureAwait(false);
    }

    public void Init()
    {

        this.host.LobbyInfo.Guests.Add(this.GuestInfo.Guid, this.GuestInfo);

        this.rpc = this.connection.Stream.CreateJsonRpc("host-guest", NetLogSession.CreateJsonRpcFailureListener("host-guest"));

        this.rpc.AddLocalRpcTarget(this);
        this.rpc.Disconnected += this.Rpc_Disconnected;
        this.rpc.SynchronizationContext = Game.SynchronizationContext;
        this.rpc.StartListening();
    }

    private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed)
        {
            return;
        }
        this.Logger.Error(e.Exception, "Abort connection: {reason}: {desc}", e.Reason, e.Description);

        this.Dispose();
    }

    // IHostClientRPC
    public Task<string> GetGUID() => Task.FromResult(this.GuestInfo.Guid);

    public Task<LobbyInfo> GetLobbyInfo() => Task.FromResult(this.host.LobbyInfo);


    public void SetName(string name)
    {
        this.Logger.Information("Set name as '{name}'", this.GuestInfo.Guid, name);
        this.GuestInfo.Name = name;
    }

    public Task SetSkinMould(string skinMould)
    {
        this.GuestInfo.SkinMould = skinMould;
        return Task.CompletedTask;
    }

    public void Quit()
    {
        this.Logger.Information("Quit.", this.GuestInfo.Guid);

        this.Dispose();
    }

    public Task<bool> CheckVersion(string version)
    {
        if (Version.Parse(version) != VersionUtils.ModVersion)
        {
            this.Logger.Error("({guid}) Dismatch version: {ver}", this.GuestInfo.Guid, version);
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ =>
            {
                if (this.rpc == null)
                {
                    return;
                }
                if (!this.rpc.IsDisposed)
                {
                    this.Dispose();
                }
            });
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    public void SetReady(bool ready)
    {
        this.Logger.Information("Set Ready: {val}", this.GuestInfo.Guid, ready);
        this.GuestInfo.IsReady = ready;
    }

    public Task Ping() => Task.CompletedTask;


    public Task<Stream> GetServerStream()
    {
        if (!this.host.LobbyInfo.CanConnectServer || this.host.Session == null)
        {
            throw new InvalidOperationException();
        }

        this.Logger.Information("Connected server");
        this.channel = this.host.Session.AllocNewChannel();
        var stream = this.channel.AsStream();

        //写入客户guid
        var guidBytes = System.Text.Encoding.UTF8.GetBytes(this.GuestInfo.Guid);
        Debug.Assert(guidBytes.Length == 36);
        stream.Write(guidBytes, 0, guidBytes.Length);
        stream.Flush();

        Debug.Assert(this.IsConnectedServer);
        return Task.FromResult(stream);
    }

    //

    private MultiplexingStream.Channel? channel;

    /// <summary>
    /// 是否连接到服务器
    /// </summary>
    public bool IsConnectedServer => this.channel != null;

    public void StartConnectServer() => Task.Delay(TimeSpan.FromSeconds(
            ModConfig.Config.Value.StartTimeout
            ), this.DisposeToken
            ).ContinueWith(_ =>
            {
                if (!this.IsConnectedServer)
                {
                    this.Logger.Error("Failed to connect server. Timeout.");
                    this.Dispose();
                }
            });

}
