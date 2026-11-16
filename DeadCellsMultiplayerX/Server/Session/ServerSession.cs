namespace DeadCellsMultiplayerX.Server.Session;

using System.IO.Pipes;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Time;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Server.Integration.Events;
using DeadCellsMultiplayerX.Server.Replication.Areas;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Replication.Entities.Mobs;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.Session.Connection;
using DeadCellsMultiplayerX.Server.Session.Interactions;
using DeadCellsMultiplayerX.Server.World.Actors;
using Microsoft.VisualStudio.Threading;
using ModCore.Events.Interfaces.Game;
using Nerdbank.Streams;

internal sealed class ServerSession : DisposableEventReceiver,
    IOnFrameUpdate,
    IOnServerEnterNewLevel
{
    private readonly AnonymousPipeClientStream outPipe = new(PipeDirection.Out, Environment.GetEnvironmentVariable("DCMP_HOST_IN_PIPE")!);
    private readonly AnonymousPipeClientStream inPipe = new(PipeDirection.In, Environment.GetEnvironmentVariable("DCMP_HOST_OUT_PIPE")!);

    private MultiplexingStream? multiplexingStream;
    private MultiplexingStream.Channel? mainChannel;
    private Stream? mainChannelReader;
    private ServerMainThread? mainThread;
    private readonly ServerTimeSystem timeSystem = new();
    private readonly Dictionary<string, ServerGuestConnection> guests = [];

    public long CurrentTimeStamp => this.timeSystem.Now;

    /// <summary>
    /// 当前已连接玩家。作为交互名册与 Ghost tick 的唯一来源。
    /// </summary>
    public IReadOnlyCollection<string> ConnectedGuestIds => this.guests.Keys;

    public IReadOnlyCollection<ServerGuestConnection> ConnectedGuests => this.guests.Values;

    /// <summary>
    /// 关卡投票/切换的网络边界与交互入口。
    /// </summary>
    internal ServerLevelTransitionGateway LevelTransitions { get; }

    public ClientFrameCollector ClientFrames { get; } = new();
    public ServerEntityRegistry Entities { get; } = new();
    public ServerEntityHandlerRegistry EntityHandlers { get; } = new();
    public ServerLevelAreaAffectRegistry AreaAffects { get; } = new();
    public EntityFrameBuilder EntityPublisher { get; }
    public WorldFramePublisher Broadcaster { get; }
    public ServerActorManager Ghosts { get; }

    public ServerSession()
    {
        this.EntityHandlers.Register(new ServerMobEntityHandler(this.Entities));
        this.EntityPublisher = new EntityFrameBuilder(this.Entities, this.EntityHandlers);
        this.Ghosts = new ServerActorManager(this);
        this.ClientFrames.CommandsReceived += this.Ghosts.HandleCommands;
        this.Broadcaster = new WorldFramePublisher(this.ClientFrames, this.EntityPublisher, this.Ghosts, this.AreaAffects);

        this.LevelTransitions = new ServerLevelTransitionGateway(this, this.Logger);
    }

    public ServerMainThread Main => this.mainThread ?? throw new InvalidOperationException();

    public async Task Init()
    {
        await Task.Yield().ConfigureAwait(false);

        this.mainThread = new();
        this.timeSystem.Start();
        this.outPipe.WriteByte(0x32);

        this.Logger.Information("Waiting host...");
        this.multiplexingStream = await MultiplexingStream.CreateAsync(
            FullDuplexStream.Splice(this.inPipe, this.outPipe),
            new MultiplexingStream.Options()
            {
                ProtocolMajorVersion = 3
            }
            );

        await Task.Delay(10);

        this.Logger.Information("Binding main channel...");

        this.mainChannel = await this.multiplexingStream.AcceptChannelAsync("main");
        this.mainChannelReader = this.mainChannel.AsStream();

        this.Logger.Information("Waiting guests...");
        await this.WaitGuestConnect();
    }

    /// <summary>
    /// 等待 Guest 连接
    /// </summary>
    /// <returns></returns>
    private async Task WaitGuestConnect()
    {
        await Task.Yield().ConfigureAwait(false);

        var reader = this.mainChannelReader
            ?? throw new InvalidOperationException("Server main channel is not initialized.");
        var mux = this.multiplexingStream
            ?? throw new InvalidOperationException("Server multiplexing stream is not initialized.");

        var numBuffer = new byte[4];
        while (true)
        {
            this.DisposeToken.ThrowIfCancellationRequested();
            await reader.ReadAtLeastAsync(
                numBuffer, 4, throwOnEndOfStream: true, this.DisposeToken);

            var channelId = BitConverter.ToInt32(numBuffer);
            if (channelId == -1)
            {
                break; // 加载完成
            }

            this.Logger.Information("Connecting from channel {id}", channelId);

            // IO stays off the game thread.  The channel/GUID handshake is
            // transport work; only the resulting registration below mutates
            // ServerSession state and must run on the game synchronization context.
            var channel = mux.AcceptChannel(channelId);
            var stream = channel.AsStream();
            var guestGuid = await StreamUtils.ReadGuestGuidAsync(stream);

            try
            {
                await this.AcceptGuestOnMainThreadAsync(stream, guestGuid);
            }
            catch (Exception ex)
            {
                this.Logger.Error(ex, "Failed to accept guest {guid}", guestGuid);
                stream.Dispose();
            }
        }
    }

    /// <summary>
    /// Registers a guest on the game thread.  <see cref="WaitGuestConnect"/>
    /// runs on an IO continuation; mutating the guest lists from there races
    /// with <see cref="IOnFrameUpdate.OnFrameUpdate"/>.
    /// </summary>
    private Task AcceptGuestOnMainThreadAsync(Stream stream, string guestGuid)
    {
        var context = ModCore.Modules.Game.SynchronizationContext ?? throw new InvalidOperationException(
                "Game synchronization context is not available; refusing to mutate server state off-thread.");

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        context.Post(_ =>
        {
            try
            {
                this.AcceptGuest(stream, guestGuid);
                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }, null);

        return completion.Task;
    }

    private void AcceptGuest(Stream stream, string guestGuid)
    {
        if (this.IsDisposed)
        {
            stream.Dispose();
            return;
        }

        if (string.IsNullOrWhiteSpace(guestGuid)
            || this.guests.ContainsKey(guestGuid))
        {
            this.Logger.Warning("Duplicate or empty guest GUID rejected: {guid}", guestGuid);
            stream.Dispose();
            return;
        }

        var sguest = new ServerGuestConnection(this, stream, guestGuid);
        sguest.GuestInfo.Guid = guestGuid;

        System.Diagnostics.Debug.Assert(
            string.Equals(sguest.GuestInfo.Guid, guestGuid, StringComparison.Ordinal),
            "GuestInfo.Guid must match the connection GUID.");

        this.guests.Add(guestGuid, sguest);
        this.ClientFrames.Register(guestGuid, sguest.RequestClientFrames);

        this.Logger.Information("Client Enter : {guid}", guestGuid);
    }


    /// <summary>
    /// 按连接 GUID 查找客户端连接
    /// </summary>
    public bool TryGetGuest(string guid, out ServerGuestConnection guest)
        => this.guests.TryGetValue(guid, out guest!);

    public void RemoveGuest(ServerGuestConnection guest)
    {
        if (guest == null)
        {
            return;
        }

        var guid = guest.ConnectionGuid;
        if (!this.guests.Remove(guid))
        {
            return;
        }

        this.ClientFrames.Unregister(guid);
        this.Ghosts.RemoveGuest(guid);
    }

    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        this.timeSystem.Tick();

        try
        {
            this.ClientFrames.Tick(this.CurrentTimeStamp);
            this.Ghosts.Tick(this.CurrentTimeStamp, this.ConnectedGuestIds);
            this.Broadcaster.Tick(dt, this.CurrentTimeStamp, [.. this.guests.Values]);
        }
        catch (Exception ex)
        {
            this.Logger.Error(ex, "World frame tick failed");
        }
    }

    void IOnServerEnterNewLevel.OnServerEnterNewLevel() => this.BeginLevelEpoch();

    /// <summary>
    /// Starts a new server world epoch for a level load.  Called both by the
    /// initial save-file load (IOnServerEnterNewLevel) and by the next-level
    /// coordinator after the server itself has transitioned.
    /// </summary>
    public void BeginLevelEpoch()
    {
        // 关卡本体已在事件前加载 怪物也已完成注册 此处不能清空寄存器
        this.AreaAffects.Clear();
        this.Broadcaster.BeginEpoch(this.CurrentTimeStamp);
        this.Ghosts.BeginEpoch(this.Broadcaster.Epoch);
    }

    protected override void MyDispose()
    {
        this.Broadcaster.Clear();
        this.ClientFrames.Clear();
        this.Entities.ResetWorld();
        this.AreaAffects.Clear();
        this.Ghosts.Clear();

        // Guests own their RPC + per-guest channel.  Dispose before the
        // transport is torn down; RemoveGuest mutates the map, so iterate a copy.
        foreach (var guest in this.guests.Values.ToArray())
        {
            this.DisposeSafely(guest, "ServerGuestConnection");
        }

        this.guests.Clear();

        this.DisposeSafely(this.mainThread, "ServerMainThread");
        this.mainThread = null;

        this.DisposeSafely(this.mainChannelReader, "Server main channel reader");
        this.mainChannelReader = null;
        this.DisposeSafely(this.mainChannel, "Server main channel");
        this.mainChannel = null;
        if (this.multiplexingStream != null)
        {
            // Dispose() is obsolete; block the sync fallback on the async API.
            try
            {
                DisposeAsyncBlocking(() => this.multiplexingStream.DisposeAsync());
            }
            catch (Exception ex)
            {
                this.Logger.Warning(ex, "Dispose failed: {Resource}", "Server multiplexing stream");
            }
            this.multiplexingStream = null;
        }

        this.DisposeSafely(this.inPipe, "Server in pipe");
        this.DisposeSafely(this.outPipe, "Server out pipe");

        base.MyDispose();
    }
}
