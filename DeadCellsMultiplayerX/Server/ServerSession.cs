using dc.en;
using DeadCellsMultiplayerX.Common;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Server.Connection;
using DeadCellsMultiplayerX.Server.Events;
using DeadCellsMultiplayerX.Server.WorldX;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts;
using DeadCellsMultiplayerX.Server.WorldX.Hubs;
using DeadCellsMultiplayerX.Utils;
using Microsoft.VisualStudio.Threading;
using ModCore.Events.Interfaces.Game;
using Nerdbank.Streams;
using System.Diagnostics;
using System.IO.Pipes;

namespace DeadCellsMultiplayerX.Server
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
        private readonly ClientWorldFrameCollector clientFrames = new();
        private readonly ServerMobRegistry mobRegistry = new();
        private readonly ServerLevelAreaAffectRegistry areaAffects = new();
        private readonly ServerMobPublisher mobPublisher;
        private readonly ServerWorldBroadcaster broadcaster;
        private readonly ServerGhostManager ghosts;

        private readonly List<SGuestConnection> guests = [];
        public readonly Dictionary<string, LevelVote> GuestsGameInfo = [];

        public long CurrentTimeStamp => timeSystem.Now;
        public ClientWorldFrameCollector ClientFrames => clientFrames;
        public ServerMobRegistry MobRegistry => mobRegistry;
        public ServerLevelAreaAffectRegistry AreaAffects => areaAffects;
        public ServerMobPublisher MobPublisher => mobPublisher;
        public ServerWorldBroadcaster Broadcaster => broadcaster;
        public ServerGhostManager Ghosts => ghosts;

        public ServerSession()
        {
            mobPublisher = new ServerMobPublisher(mobRegistry);
            ghosts = new ServerGhostManager(this);
            broadcaster = new ServerWorldBroadcaster(
                clientFrames, mobPublisher, ghosts, areaAffects);
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

            mainThread = new(this);
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
            Debug.Assert(mainChannelReader != null);
            Debug.Assert(multiplexingStream != null);

            byte[] numBuffer = new byte[4];
            while (true)
            {
                await mainChannelReader.ReadAtLeastAsync(numBuffer, 4, throwOnEndOfStream: true);

                var channelId = BitConverter.ToInt32(numBuffer);
                if (channelId == -1)
                {
                    break; // 加载完成
                }

                Logger.Information("Connecting from channel {id}", channelId);

                var channel = multiplexingStream.AcceptChannel(channelId);
                var stream = channel.AsStream();

                string guestGuid = await StreamUtils.ReadGuestGuidAsync(stream);

                var sguest = new SGuestConnection(this, stream);
                sguest.GuestInfo.Guid = guestGuid;

                guests.Add(sguest);

                var gameInfo = new LevelVote
                {
                    PlyerID = guestGuid,
                };
                GuestsGameInfo.Add(guestGuid, gameInfo);

                clientFrames.Register(guestGuid, sguest.RequestClientFrames);

                Logger.Information("Client Enter : {guid}", guestGuid);
            }
        }


        /// <summary>
        /// 通知所有客户端切换关卡
        /// </summary>
        /// <param name="levelid"></param>
        /// <returns></returns>
        public Task NoticeGuestsEnterNextLevel(string levelid)
        {
            foreach (var sGuest in guests)
            {
                sGuest.guest.EnterNextLevel(levelid);
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
        public bool TryGetGuest(string guid, out SGuestConnection guest)
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

        public void RemoveGuest(SGuestConnection guest)
        {
            if (guest == null) return;

            guests.Remove(guest);

            string guid = guest.GuestInfo.Guid;
            if (string.IsNullOrEmpty(guid)) return;

            GuestsGameInfo.Remove(guid);
            clientFrames.Unregister(guid);
            ghosts.RemoveGuest(guid);
        }

        /// <summary>
        /// 可靠地把 mob 命中 Ghost 的事件送到 owner
        /// </summary>
        public void SendGhostHit(GhostHitEvent hit)
        {
            if (hit == null || string.IsNullOrEmpty(hit.Guid))
                return;

            if (!TryGetGuest(hit.Guid, out var guest) || guest.IsDisposed)
            {
                Logger.Warning(
                    "Ghost hit owner unavailable: guid={Guid} damage={Damage}",
                    hit.Guid, hit.Damage);
                return;
            }

            Logger.Information(
                "Ghost hit sent: guid={Guid} damage={Damage}",
                hit.Guid, hit.Damage);

            _ = guest.guest.ApplyGhostHit(hit).ContinueWith(
                t => Logger.Warning(t.Exception, "ApplyGhostHit failed"),
                TaskContinuationOptions.OnlyOnFaulted);
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
            // 关卡本体已在事件前加载 怪物也已完成注册 此处不能清空寄存器
            areaAffects.Clear();
            broadcaster.BeginEpoch(CurrentTimeStamp);
            ghosts.BeginEpoch(broadcaster.Epoch);
        }

        protected override void MyDispose()
        {
            broadcaster.Clear();
            clientFrames.Clear();
            mobRegistry.BeginEpoch();
            areaAffects.Clear();
            ghosts.Clear();
            base.MyDispose();
        }
    }
}
