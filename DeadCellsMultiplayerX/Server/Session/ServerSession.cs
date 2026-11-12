using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Server.Session.Connection;
using DeadCellsMultiplayerX.Server.Integration.Events;
using DeadCellsMultiplayerX.Server.World.Actors;
using DeadCellsMultiplayerX.Server.Replication.Areas;
using DeadCellsMultiplayerX.Server.Replication.Entities;
using DeadCellsMultiplayerX.Server.Replication.Entities.Mobs;
using DeadCellsMultiplayerX.Server.Replication.Frames;
using Microsoft.VisualStudio.Threading;
using ModCore.Events.Interfaces.Game;
using Nerdbank.Streams;
using System.IO.Pipes;

using DeadCellsMultiplayerX.Contracts.Lobby;

using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Time;
using DeadCellsMultiplayerX.Networking.Transport;
namespace DeadCellsMultiplayerX.Server.Session
{
    internal class ServerSession : DisposableEventReceiver,
        IOnFrameUpdate,
        IOnServerEnterNewLevel
    {
        public AnonymousPipeClientStream outPipe = new(PipeDirection.Out, Environment.GetEnvironmentVariable("DCMP_HOST_IN_PIPE")!);
        public AnonymousPipeClientStream inPipe = new(PipeDirection.In, Environment.GetEnvironmentVariable("DCMP_HOST_OUT_PIPE")!);

        private MultiplexingStream? multiplexingStream;
        private MultiplexingStream.Channel? mainChannel;
        private Stream? mainChannelReader;
        private ServerMainThread? mainThread;
        private readonly ServerTimeSystem timeSystem = new();
        private readonly ClientFrameCollector clientFrames = new();
        private readonly ServerEntityRegistry entities = new();
        private readonly ServerEntityHandlerRegistry entityHandlers = new();
        private readonly ServerLevelAreaAffectRegistry areaAffects = new();
        private readonly EntityFrameBuilder entityPublisher;
        private readonly WorldFramePublisher broadcaster;
        private readonly ServerActorManager ghosts;

        private readonly List<ServerGuestConnection> guests = [];
        public readonly Dictionary<string, LevelVote> GuestsGameInfo = [];

        /// <summary>
        /// One coordinator per session, not per guest connection.  A per-guest
        /// gate lets every voter run the transition simultaneously.
        /// </summary>
        private int levelTransitionGate;

        public long CurrentTimeStamp => timeSystem.Now;
        public ClientFrameCollector ClientFrames => clientFrames;
        public ServerEntityRegistry Entities => entities;
        public ServerEntityHandlerRegistry EntityHandlers => entityHandlers;
        public ServerLevelAreaAffectRegistry AreaAffects => areaAffects;
        public EntityFrameBuilder EntityPublisher => entityPublisher;
        public WorldFramePublisher Broadcaster => broadcaster;
        public ServerActorManager Ghosts => ghosts;

        public ServerSession()
        {
            entityHandlers.Register(new ServerMobEntityHandler(entities));
            entityPublisher = new EntityFrameBuilder(entities, entityHandlers);
            ghosts = new ServerActorManager(this);
            broadcaster = new WorldFramePublisher(
                clientFrames, entityPublisher, ghosts, areaAffects);
        }

        /// <summary>
        /// 处理客户端上报的英雄动作
        /// </summary>
        public void HandleHeroAction(string senderGuid, HeroAction? request)
        {
            ghosts.HandleAction(senderGuid, request);
        }

        public ServerMainThread Main => mainThread ?? throw new InvalidOperationException();

        public async Task Init()
        {
            await Task.Yield().ConfigureAwait(false);

            mainThread = new();
            timeSystem.Start();
            outPipe.WriteByte(0x32);

            Logger.Information("Waiting host...");
            multiplexingStream = await MultiplexingStream.CreateAsync(
                FullDuplexStream.Splice(inPipe, outPipe),
                new MultiplexingStream.Options()
                {
                    ProtocolMajorVersion = 3
                }
                );

            await Task.Delay(10);

            Logger.Information("Binding main channel...");

            mainChannel = await multiplexingStream.AcceptChannelAsync("main");
            mainChannelReader = mainChannel.AsStream();

            Logger.Information("Waiting guests...");
            await WaitGuestConnect();
        }

        /// <summary>
        /// 等待 Guest 连接
        /// </summary>
        /// <returns></returns>
        private async Task WaitGuestConnect()
        {
            await Task.Yield().ConfigureAwait(false);

            var reader = mainChannelReader
                ?? throw new InvalidOperationException("Server main channel is not initialized.");
            var mux = multiplexingStream
                ?? throw new InvalidOperationException("Server multiplexing stream is not initialized.");

            byte[] numBuffer = new byte[4];
            while (true)
            {
                DisposeToken.ThrowIfCancellationRequested();
                await reader.ReadAtLeastAsync(
                    numBuffer, 4, throwOnEndOfStream: true, DisposeToken);

                var channelId = BitConverter.ToInt32(numBuffer);
                if (channelId == -1)
                {
                    break; // 加载完成
                }

                Logger.Information("Connecting from channel {id}", channelId);

                // IO stays off the game thread.  The channel/GUID handshake is
                // transport work; only the resulting registration below mutates
                // ServerSession state and must run on the game synchronization context.
                var channel = mux.AcceptChannel(channelId);
                var stream = channel.AsStream();
                string guestGuid = await StreamUtils.ReadGuestGuidAsync(stream);

                try
                {
                    await AcceptGuestOnMainThreadAsync(stream, guestGuid);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to accept guest {guid}", guestGuid);
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
            var context = ModCore.Modules.Game.SynchronizationContext;
            if (context == null)
            {
                throw new InvalidOperationException(
                    "Game synchronization context is not available; refusing to mutate server state off-thread.");
            }

            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            context.Post(_ =>
            {
                try
                {
                    AcceptGuest(stream, guestGuid);
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
            if (IsDisposed)
            {
                stream.Dispose();
                return;
            }

            if (string.IsNullOrWhiteSpace(guestGuid)
                || guests.Any(g => string.Equals(
                    g.GuestInfo.Guid, guestGuid, StringComparison.Ordinal))
                || GuestsGameInfo.ContainsKey(guestGuid))
            {
                Logger.Warning("Duplicate or empty guest GUID rejected: {guid}", guestGuid);
                stream.Dispose();
                return;
            }

            var sguest = new ServerGuestConnection(this, stream);
            sguest.GuestInfo.Guid = guestGuid;

            guests.Add(sguest);
            GuestsGameInfo.Add(guestGuid, new LevelVote { PlyerID = guestGuid });
            clientFrames.Register(guestGuid, sguest.RequestClientFrames);

            Logger.Information("Client Enter : {guid}", guestGuid);
        }


        /// <summary>
        /// Claims the single server-side level transition coordinator role.
        /// </summary>
        public bool TryBeginLevelTransition()
            => Interlocked.CompareExchange(ref levelTransitionGate, 1, 0) == 0;

        /// <summary>
        /// Releases the coordinator role in every exit path.
        /// </summary>
        public void EndLevelTransition()
            => Interlocked.Exchange(ref levelTransitionGate, 0);

        /// <summary>
        /// 通知所有客户端切换关卡
        /// </summary>
        /// <param name="levelid"></param>
        /// <returns></returns>
        public Task NoticeGuestsEnterNextLevel(string levelid)
        {
            foreach (var sGuest in guests)
            {
                try
                {
                    _ = sGuest.guest.EnterNextLevel(levelid)
                        .ContinueWith(
                            t => Logger.Warning(
                                t.Exception,
                                "EnterNextLevel RPC failed: {Guid}",
                                sGuest.GuestInfo.Guid),
                            TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (Exception ex)
                {
                    Logger.Warning(
                        ex,
                        "EnterNextLevel dispatch failed: {Guid}",
                        sGuest.GuestInfo.Guid);
                }
            }

            foreach (var guest in GuestsGameInfo)
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
            foreach (var item in guests)
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
            if (guest == null) return;

            guests.Remove(guest);

            string guid = guest.GuestInfo.Guid;
            if (string.IsNullOrEmpty(guid)) return;

            GuestsGameInfo.Remove(guid);
            clientFrames.Unregister(guid);
            ghosts.RemoveGuest(guid);
        }

        void IOnFrameUpdate.OnFrameUpdate(double dt)
        {
            timeSystem.Tick();

            try
            {
                clientFrames.Tick(CurrentTimeStamp);
                ghosts.Tick(dt, CurrentTimeStamp, GuestsGameInfo.Keys);
                broadcaster.Tick(dt, CurrentTimeStamp, guests.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "World frame tick failed");
            }
        }

        void IOnServerEnterNewLevel.OnServerEnterNewLevel()
        {
            BeginLevelEpoch();
        }

        /// <summary>
        /// Starts a new server world epoch for a level load.  Called both by the
        /// initial save-file load (IOnServerEnterNewLevel) and by the next-level
        /// coordinator after the server itself has transitioned.
        /// </summary>
        public void BeginLevelEpoch()
        {
            // 关卡本体已在事件前加载 怪物也已完成注册 此处不能清空寄存器
            areaAffects.Clear();
            broadcaster.BeginEpoch(CurrentTimeStamp);
            ghosts.BeginEpoch(broadcaster.Epoch);
        }

        protected override void MyDispose()
        {
            broadcaster.Clear();
            clientFrames.Clear();
            entities.ResetWorld();
            areaAffects.Clear();
            ghosts.Clear();

            // Guests own their RPC + per-guest channel.  Dispose before the
            // transport is torn down; RemoveGuest mutates the list, so iterate a copy.
            foreach (var guest in guests.ToArray())
                DisposeSafely(guest, "ServerGuestConnection");
            guests.Clear();
            GuestsGameInfo.Clear();

            DisposeSafely(mainThread, "ServerMainThread");
            mainThread = null;

            DisposeSafely(mainChannelReader, "Server main channel reader");
            mainChannelReader = null;
            DisposeSafely(mainChannel, "Server main channel");
            mainChannel = null;
            if (multiplexingStream != null)
            {
                // Dispose() is obsolete; block the sync fallback on the async API.
                try
                {
                    DisposeAsyncBlocking(() => multiplexingStream.DisposeAsync());
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Dispose failed: {Resource}", "Server multiplexing stream");
                }
                multiplexingStream = null;
            }

            DisposeSafely(inPipe, "Server in pipe");
            DisposeSafely(outPipe, "Server out pipe");

            base.MyDispose();
        }
    }
}
