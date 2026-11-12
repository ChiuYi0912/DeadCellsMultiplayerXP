using dc.cine;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Client.World;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using StreamJsonRpc;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.SourceGenerator;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Client.Integration.Hooks;
namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    /// <summary>
    /// 访客的客户端会话。
    /// 思路：负责连接、协议握手、时间同步、本地采集上传与世界帧分发；
    /// 远端动作的表现由 ClientWorld/ClientReplayProxyManager 的 presenter 链处理，本类不直接做动作分发。
    /// </summary>
    internal partial class GuestClientSession(GuestClient client, Stream serverStream) : ClientSession,
        IGuestRPC,
        IOnFrameUpdate,
        IOnLocalHeroPostUpdate,
        IOnLocalHeroInitDone,
        IHeroActionSink
    {
        private JsonRpc? rpc;
        private IServerRPC server = null!;
        private byte[]? saveData;
        private GuestHookSession? hookSession;


        /// <summary>
        /// 上传通道是否已判定为断开
        /// </summary>
        public bool UploadBroken => Uploader.UploadBroken;

        public IServerRPC Server => server ?? throw new InvalidOperationException();

        public dc.pr.Game Game => dc.pr.Game.Class.ME;
        public GuestClient Client { get; private set; } = null!;

        internal ClientWorld? ClientWorld => remoteWorld;
        private ClientWorld? remoteWorld;
        private LocalActionCapture? localCapture;
        private LocalCombatPrediction? prediction;
        private CombatDeathGate? deathGate;

        /// <summary>
        /// 本地武器命中预测
        /// </summary>
        internal LocalCombatPrediction Prediction => prediction ?? throw new InvalidOperationException();

        /// <summary>
        /// 允许客户端 mob 执行死亡的闸门
        /// </summary>
        internal CombatDeathGate DeathGate => deathGate ?? throw new InvalidOperationException();

        public volatile bool serverDrivenTransition = false;

        /// <summary>
        /// 受击表现调用深度 防止 Mob.onDamage 内部再次触发命中时递归
        /// </summary>
        private int replayVisualDepth;

        private GuestFrameUploader? uploader;

        private GuestFrameUploader Uploader
            => uploader ??= new GuestFrameUploader(
                () => IsDisposed,
                () => Client.Guid,
                () => SyncedTimeMs,
                frame => server.UploadClientWorldFrame(frame),
                Logger);

        /// <summary>
        /// 仅用于统计世界帧序列化大小
        /// </summary>
        private static readonly MessagePackSerializer FrameSizeSerializer = new();

        private int lastWorldFrameBytes;

        /// <summary>
        /// 最近一帧世界帧的 MessagePack 负载字节数
        /// </summary>
        public int LastWorldFrameBytes => lastWorldFrameBytes;

        public override async Task Init()
        {
            deathGate = new CombatDeathGate();
            prediction = new LocalCombatPrediction(this);
            localCapture = new LocalActionCapture(this, this);
            localCapture.Init();

            InitHooks(deathGate);

#if DEBUG
            DebugHUD.EnsureCreated();
#endif

            var handshake = await GuestHandshake.RunAsync(
                this,
                serverStream,
                client,
                Rpc_Disconnected,
                Logger);

            if (handshake == null)
            {
                Dispose();
                return;
            }

            rpc = handshake.Rpc;
            server = handshake.Server;
            Client = client;
        }

        private GuestWorldLoader? worldLoader;

        private GuestWorldLoader WorldLoader
            => worldLoader ??= new GuestWorldLoader(
                DisposeToken,
                ReplaceWorld,
                () => Uploader.ResetLocalHeroState(),
                data => saveData = data,
                client,
                Logger);

        /// <summary>
        /// 载入新 level 并初始化
        /// </summary>
        /// <param name="saveData"></param>
        /// <returns></returns>
        public Task EnterNewLevel(byte[] saveData)
            => WorldLoader.EnterNewLevel(saveData);

        private ClientWorld ReplaceWorld()
        {
            prediction?.Clear();
            deathGate?.Clear();
            remoteWorld?.Dispose();
            remoteWorld = new ClientWorld(this, client.Guid);
            return remoteWorld;
        }


        void IOnFrameUpdate.OnFrameUpdate(double dt)
        {
            // 同步 TimeStamp
            TickTimeSystem();

            // 世界帧 NACK 调度/超时
            remoteWorld?.Tick(SyncedTimeMs);
        }

        /// <summary>
        /// 服务端调用,用于通知当前客户切换关卡
        /// </summary>
        /// <param name="levelid"></param>
        /// <returns></returns>
        public Task EnterNextLevel(string levelid)
        {
            Logger.Information("Entering next level: {LevelId}", levelid);
            serverDrivenTransition = true;
            try
            {
                LevelTransition.Class.@goto.Invoke(levelid.AsHaxeString());
            }
            finally
            {
                serverDrivenTransition = false;
            }
            return Task.CompletedTask;
        }

        void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
        {
            Uploader.CaptureAndUpload(hero);
        }

        private void TickTimeSystem()
        {
            Clock.Tick();
        }

        /// <summary>
        /// 服务端每 tick 推送完整世界帧
        /// </summary>
        public Task SyncWorldFrame(WorldFrame frame)
        {
            if (frame == null) return Task.CompletedTask;

#if DEBUG
            if (WorldFrameDebug.ConsumeDropServerFrame())
                return Task.CompletedTask;
            if (DebugFlags.Get("netstat-Measure"))
                MeasureWorldFrame(frame);
#endif

            ObserveWorldEpoch(frame.Epoch);
            ObserveServerTime((long)frame.ServerTimeMs);
            remoteWorld?.OnWorldFrame(frame);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 统计世界帧的 MessagePack 负载大小
        /// </summary>
        private void MeasureWorldFrame(WorldFrame frame)
        {
            var shape = TypeShapeProvider_DeadCellsMultiplayerX.Default
                .GetTypeShapeOrThrow<WorldFrame>();

            lastWorldFrameBytes = FrameSizeSerializer
                .Serialize(frame, shape)
                .Length;
        }

        /// <summary>
        /// 服务端请求补发本地上传帧
        /// </summary>
        public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
            => Task.FromResult(Uploader.BuildBundle(request));

        internal void ObserveWorldEpoch(int epoch) => Uploader.SetEpoch(epoch);

        internal void ObserveServerTime(long serverTimeMs)
        {
            Clock.ObserveServerTime(serverTimeMs);
        }

        void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
        {
            Uploader.ResetLocalHeroState();
        }

        Task IHeroActionSink.SubmitHeroAction(HeroAction action)
        {
            return server.SubmitHeroAction(action);
        }

        /// <summary>
        /// 创建会话级 Hook 适配器；适配器只拿到完成工作所需的窄接口。
        /// </summary>
        private void InitHooks(CombatDeathGate deathGate)
        {
            var sink = new GuestSessionHookSink(
                new GuestCombatHookAdapter(
                    mob => remoteWorld?.IsRemoteMob(mob) == true,
                    () => remoteWorld?.IsApplyingServerAreaAffect == true,
                    PlayHitVisual),
                new GuestMobHookAdapter(
                    () => remoteWorld?.MobModifiers,
                    mob => remoteWorld?.IsRemoteMob(mob) == true,
                    mob => remoteWorld?.IsDeathSuppressed(mob) == true,
                    () => SyncedTimeMs,
                    deathGate),
                new GuestSaveHookAdapter(
                    () => saveData),
                new GuestGameHookAdapter(
                    Logger,
                    () => ClientRuntime.Instance.CurrentGuestClient,
                    client.Quit,
                    Dispose));

            hookSession = new GuestHookSession(sink);
        }

        /// <summary>
        /// 播放远端 mob 的受击表现，避免 Mob.onDamage 内部递归
        /// </summary>
        internal void PlayHitVisual(AttackData atk, Mob mob)
        {
            if (mob == null || mob.destroyed || replayVisualDepth != 0)
                return;

            Logger.Verbose(
                "Puppet hit visual: target={Target} dmg={Dmg} hit={Hit}",
                mob.type, atk.finalDmg, atk.hitResult);

            replayVisualDepth++;
            try
            {
                mob.onDamage(atk);
            }
            finally
            {
                replayVisualDepth--;
            }
        }

        /// <summary>
        /// 强制客户端 mob 执行原生死亡
        /// </summary>
        internal void ForceNativeDeath(Mob mob)
        {
            if (mob == null || mob.destroyed)
                return;

            using (DeathGate.Allow(mob))
            {
                mob.life = 0;
                mob.onDie();
            }

            if (!mob.destroyed)
                mob.destroy();
        }

        internal void DebugCastSkill(string itemId, int slot)
            => localCapture?.DebugCastSkill(itemId, slot);

        internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
            => localCapture?.DebugUseWeapon(itemId, slot, cycle);
    }
}
