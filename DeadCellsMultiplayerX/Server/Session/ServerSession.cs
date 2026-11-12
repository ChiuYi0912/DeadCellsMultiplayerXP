namespace DeadCellsMultiplayerX.Server.Session;

using System.IO.Pipes;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Time;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Server.Integration.Events;
using DeadCellsMultiplayerX.Server.Replication.Areas;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Replication.Entities.Mobs;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using DeadCellsMultiplayerX.Server.Session.Connection;
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
    private readonly List<ServerGuestConnection> guests = [];
    public Dictionary<string, LevelVote> GuestsGameInfo { get; } = [];

    /// <summary>
    /// One coordinator per session, not per guest connection.  A per-guest
    /// gate lets every voter run the transition simultaneously.
    /// </summary>
    private int levelTransitionGate;

    public long CurrentTimeStamp => this.timeSystem.Now;
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
        this.Broadcaster = new WorldFramePublisher(
            this.ClientFrames, this.EntityPublisher, this.Ghosts, this.AreaAffects);
    }

    /// <summary>
    /// 处理客户端上报的英雄动作
    /// </summary>
    public void HandleHeroAction(string senderGuid, HeroAction? request) => this.Ghosts.HandleAction(senderGuid, request);

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
            || this.guests.Any(g => string.Equals(
                g.GuestInfo.Guid, guestGuid, StringComparison.Ordinal))
            || this.GuestsGameInfo.ContainsKey(guestGuid))
        {
            this.Logger.Warning("Duplicate or empty guest GUID rejected: {guid}", guestGuid);
            stream.Dispose();
            return;
        }

        var sguest = new ServerGuestConnection(this, stream);
        sguest.GuestInfo.Guid = guestGuid;

        this.guests.Add(sguest);
        this.GuestsGameInfo.Add(guestGuid, new LevelVote { PlyerID = guestGuid });
        this.ClientFrames.Register(guestGuid, sguest.RequestClientFrames);

        this.Logger.Information("Client Enter : {guid}", guestGuid);
    }


    /// <summary>
    /// Claims the single server-side level transition coordinator role.
    /// </summary>
    public bool TryBeginLevelTransition()
        => Interlocked.CompareExchange(ref this.levelTransitionGate, 1, 0) == 0;

    /// <summary>
    /// Releases the coordinator role in every exit path.
    /// </summary>
    public void EndLevelTransition()
        => Interlocked.Exchange(ref this.levelTransitionGate, 0);

    /// <summary>
    /// 通知所有客户端切换关卡
    /// </summary>
    /// <param name="levelid"></param>
    /// <returns></returns>
    public Task NoticeGuestsEnterNextLevel(string levelid)
    {
        foreach (var sGuest in this.guests)
        {
            try
            {
                _ = sGuest.Guest.EnterNextLevel(levelid)
                    .ContinueWith(
                        t => this.Logger.Warning(
                            t.Exception,
                            "EnterNextLevel RPC failed: {Guid}",
                            sGuest.GuestInfo.Guid),
                        TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                this.Logger.Warning(
                    ex,
                    "EnterNextLevel dispatch failed: {Guid}",
                    sGuest.GuestInfo.Guid);
            }
        }

        foreach (var guest in this.GuestsGameInfo)
        {
            guest.Value.LevelSelected = false;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 按 GUID 查找客户端连接
    /// </summary>
    public bool TryGetGuest(string guid, out ServerGuestConnection guest)
    {
        foreach (var item in this.guests)
        {
            if (string.Equals(item.GuestInfo.Guid, guid, StringComparison.Ordinal))
            {
                guest = item;
                return true;
            }
        }

        guest = null!;
        return false;
    }

    public void RemoveGuest(ServerGuestConnection guest)
    {
        if (guest == null)
        {
            return;
        }

        this.guests.Remove(guest);

        var guid = guest.GuestInfo.Guid;
        if (string.IsNullOrEmpty(guid))
        {
            return;
        }

        this.GuestsGameInfo.Remove(guid);
        this.ClientFrames.Unregister(guid);
        this.Ghosts.RemoveGuest(guid);
    }

    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        this.timeSystem.Tick();

        try
        {
            this.ClientFrames.Tick(this.CurrentTimeStamp);
            this.Ghosts.Tick(dt, this.CurrentTimeStamp, this.GuestsGameInfo.Keys);
            this.Broadcaster.Tick(dt, this.CurrentTimeStamp, this.guests.ToArray());
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
        // transport is torn down; RemoveGuest mutates the list, so iterate a copy.
        foreach (var guest in this.guests.ToArray())
        {
            this.DisposeSafely(guest, "ServerGuestConnection");
        }

        this.guests.Clear();
        this.GuestsGameInfo.Clear();

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
