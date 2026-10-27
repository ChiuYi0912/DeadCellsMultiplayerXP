using dc;
using dc.cine;
using dc.en;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using DeadCellsMultiplayerX.Client.Event;
using DeadCellsMultiplayerX.Client.Guest.Ghost;
using DeadCellsMultiplayerX.Client.Guest.WorldX;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Remote;
using DeadCellsMultiplayerX.Client.Guest.WorldX.Snapshot;
using DeadCellsMultiplayerX.Client.Host;
using DeadCellsMultiplayerX.Common.Data;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Server;
using DeadCellsMultiplayerX.Utils;
using Hashlink.Proxy.Clousre;
using ModCore;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using ModCore.Events.Interfaces.Game.Hero;
using ModCore.Modules;
using ModCore.Utilities;
using StreamJsonRpc;
using System.Diagnostics;

namespace DeadCellsMultiplayerX.Client.Guest
{
    /// <summary>
    /// 访客的客户端 session
    /// </summary>
    internal class GuestClientSession(GuestClient client, Stream serverStream) : ClientSession,
        IGuestRPC,
        IOnFrameUpdate,
        IOnLocalHeroPostUpdate,
        IOnLocalHeroInitDone
    {
        private JsonRpc? rpc;
        private IServerRPC server = null!;
        private bool isOwner = false;
        private byte[]? saveData;
        private ClientReplicator? replicator;
        private readonly List<HashlinkHooks.HookHandle> hooks = [];

        #region Time
        private readonly ClientTimeSystem timeSystem = new();

        // NTP 采样调度
        private Task? ntpTask;
        private long lastNtpRequestMs;
        private const long NtpIntervalMs = 500;

        /// <summary>
        /// 与服务端同步的时间轴
        /// </summary>
        public long SyncedTimeMs => timeSystem.SyncedTime;
        #endregion

        public IServerRPC Server => server ?? throw new InvalidOperationException();

        public dc.pr.Game Game => dc.pr.Game.Class.ME;
        public GuestClient Client { get; private set; } = null!;

        internal RemoteWorld? RemoteWorld => remoteWorld;
        private RemoteWorld? remoteWorld;
        private LocalGhostCapture? localCapture;
        private LocalCombatPrediction? prediction;
        private GhostCombatResolver? ghostCombat;
        private CombatDeathGate? deathGate;

        /// <summary>
        /// 本地武器命中预测
        /// </summary>
        internal LocalCombatPrediction Prediction => prediction
            ?? throw new InvalidOperationException("Combat prediction not initialized.");

        /// <summary>
        /// 其他玩家 ghost 武器命中的帧结算
        /// </summary>
        internal GhostCombatResolver GhostCombat => ghostCombat
            ?? throw new InvalidOperationException("Ghost combat not initialized.");

        /// <summary>
        /// 允许客户端 mob 执行死亡的闸门
        /// </summary>
        internal CombatDeathGate DeathGate => deathGate
            ?? throw new InvalidOperationException("Death gate not initialized.");

        public volatile bool serverDrivenTransition = false;
        private readonly ClientWorldSendBuffer sendBuffer = new();
        private long lastServerFrameTimeMs = long.MinValue;

        /// <summary>
        /// 受击表现调用深度 防止 Mob.onDamage 内部再次触发命中时递归
        /// </summary>
        private int replayVisualDepth;

        // 同一时间只保留一个在途请求,避免连接异常时 RPC 队列堆积
        private Task? uploadTask;
        private long lastUploadErrorLogMs;
        private bool uploadBroken;

        public override async Task Init()
        {
            InitHooks();

            rpc = serverStream.CreateJsonRpc();
            rpc.AddLocalRpcTarget(this);

            server = rpc.Attach<IServerRPC>();

            rpc.SynchronizationContext = ModCore.Modules.Game.SynchronizationContext;

            rpc.Disconnected += Rpc_Disconnected;
            rpc.StartListening();

            if (!await server.CheckVersion(
               VersionUtils.ModVersion.ToString()
               ))
            {
                Logger.Information("Failed to connect lobby. Dismatch version.");
                Dispose();
                return;
            }

            Debug.Assert(client.LobbyInfo != null);

            Client = client;

            if (client.LobbyInfo.Owner == client.Guid)
            {
                isOwner = true;
            }

            Logger.Information("Updating guest info...");

            await server.SetGuestInfo(client.LobbyInfo.Guests[client.Guid]);

            if (isOwner)
            {
                //上传存档到服务器
                Logger.Information("Updating savedata...");
                var fp = System.IO.Path.GetFullPath("save/" + Save.Class.fileName(null).ToString());

                //必须使用现有存档进行联机
                Debug.Assert(System.IO.File.Exists(fp));

                await server.UploadSavedata(await System.IO.File.ReadAllBytesAsync(fp));
            }
        }

        private void InitWorld()
        {
            prediction?.Clear();
            deathGate?.Clear();
            remoteWorld?.Dispose();
            remoteWorld = new RemoteWorld(this, client.Guid);
            sendBuffer.ResetLocalHeroState();
        }

        private void InitHooks()
        {
            deathGate = new CombatDeathGate();
            ghostCombat = new GhostCombatResolver(this);
            prediction = new LocalCombatPrediction(this);

            Hook__Save.save += Hook__Save_save;
            Hook__File.getBytes += Hook__File_getBytes;

            if (GameInfo.Platform == GameInfo.PlatformKind.Steam)
            {
                HashlinkHooks.Instance.CreateHook("tool.$File", "getSteamCloudStatus", Hook__File_getSteamCloudStatus);
                HashlinkHooks.Instance.CreateHook("tool.$File", "saveSteamCloudStatus", Hook__File_saveSteamCloudStatus);
            }

            Hook_Game.onDispose += Hook_Game_onDispose;
            Hook_Mob.aiLocked += Hook_Mob_aiLocked;
            Hook_Mob.onDie += Hook_Mob_onDie;
            Hook_Game.init += Hook_Game_init;
            Hook_Power.update += Hook_Power_update;
            Hook__AttackUtils.applyHitResult += Hook_AttackUtils_applyHitResult;
            Hook__AttackUtils.postHitResultUpdate += Hook_AttackUtils_postHitResultUpdate;
            Hook__AttackUtils.createFromHeroAndHit += Hook_AttackUtils_createFromHeroAndHit;
            Hook_LevelAreaAffect.init += Hook_LevelAreaAffect_init;
            Hook_LevelAreaAffect.applyAnotherAffect += Hook_LevelAreaAffect_applyAnotherAffect;
            localCapture = new LocalGhostCapture(this);
            localCapture.Init();
        }

        /// <summary>
        /// 只允许服务端快照应用 LevelAreaAffect
        /// </summary>
        private void Hook_LevelAreaAffect_init(
            Hook_LevelAreaAffect.orig_init orig,
            dc.tool.LevelAreaAffect self,
            int x,
            int y,
            double durationS,
            int a,
            double aDurationS,
            double aValue,
            dc.hl.types.ArrayObj affixes)
        {
            var manager = remoteWorld?.AreaAffects;
            if (manager == null || !manager.IsApplyingServer)
                return;

            orig(self, x, y, durationS, a, aDurationS, aValue, affixes);
        }

        /// <summary>
        /// 只允许服务端快照应用
        /// </summary>
        private void Hook_LevelAreaAffect_applyAnotherAffect(
            Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
            dc.tool.LevelAreaAffect self,
            double a,
            int aDurationS,
            double aValue,
            double affixes,
            dc.hl.types.ArrayObj prev)
        {
            var manager = remoteWorld?.AreaAffects;
            if (manager == null || !manager.IsApplyingServer)
                return;

            orig(self, a, aDurationS, aValue, affixes, prev);
        }

        private void Hook_Power_update(Hook_Power.orig_update orig, Power self)
        {
            if (self == null) return;

            var owner = self.owner;
            if (owner == null || owner._level == null)
            {
                try
                {
                    self.destroy();
                }
                catch
                {
                    // 无法安全销毁时跳过 update
                }

                return;
            }

            orig(self);
        }


        /// <summary>
        /// 客户端对 mob 的命中只触发表现
        /// 跳过 attackTarget.applyHit 与 onHit 回调 避免本地扣血/死亡/affect
        /// 服务端快照是 mob 状态唯一权威
        /// </summary>
        private void Hook_AttackUtils_applyHitResult(
            Hook__AttackUtils.orig_applyHitResult orig,
            AttackData atk,
            Entity target)
        {
            if (target is not Mob mob || remoteWorld?.IsRemoteMob(mob) != true)
            {
                orig(atk, target);
                return;
            }

            if (replayVisualDepth != 0
                || atk == null
                || !atk.isSuccess())
            {
                return;
            }

            var receiver = remoteWorld.Mobs.Receiver;

            if (receiver.TryGetNetId(mob, out uint netId)
                && Prediction.IsPredictedDead(netId))
            {
                return;
            }

            GhostReplayGuard.MarkVisualHit(mob);

            // ghost 的服务端事件已经到达，使用同帧状态结算
            if (GhostCombat.TryHandleHit(atk, mob))
                return;

            // 本地 客户 致死走真实 orig，非致死走轻量预测
            if (Prediction.TryHandleLocalHit(
                    atk, mob,
                    () => orig(atk, target),
                    () => PlayHitVisual(atk, mob)))
            {
                return;
            }

            PlayHitVisual(atk, mob);
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

        private void Hook_AttackUtils_postHitResultUpdate(
            Hook__AttackUtils.orig_postHitResultUpdate orig,
            AttackData atk,
            Entity target)
        {
            if (target is Mob mob && remoteWorld?.IsRemoteMob(mob) == true)
                return;

            orig(atk, target);
        }

        /// <summary>
        /// 拦截本地伤害
        /// </summary>
        private AttackData Hook_AttackUtils_createFromHeroAndHit(
            Hook__AttackUtils.orig_createFromHeroAndHit orig,
            Entity parent,
            object baseDmg,
            int? data,
            Entity target)
        {
            bool remoteTarget = target is RemoteHero
                    || (target is Mob mob
                    && remoteWorld?.IsRemoteMob(mob) == true);

            if (parent == null && remoteTarget)
            {
                return null!;
            }

            return orig(parent, baseDmg, data, target);
        }


        internal void DebugCastSkill(string itemId, int slot)
            => localCapture?.DebugCastSkill(itemId, slot);

        internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
            => localCapture?.DebugUseWeapon(itemId, slot, cycle);

        private void Hook_Game_init(Hook_Game.orig_init orig, dc.pr.Game self)
        {
            orig(self);
            self.infection.destroy();
        }

        private bool Hook_Mob_aiLocked(Hook_Mob.orig_aiLocked orig, Mob self)
        {
            return true;
        }

        private void Hook_Mob_onDie(Hook_Mob.orig_onDie orig, Mob self)
        {
            // 本地预测由服务端确认死亡时允许远端 mob 执行死亡
            if (DeathGate.IsAllowed(self))
            {
                orig(self);
                return;
            }

            // 其余远端怪物与正在清理的本地怪物不执行本地死亡掉落
            if (remoteWorld?.IsRemoteMob(self) == true
                || remoteWorld?.IsDeathSuppressed(self) == true)
            {
                return;
            }

            orig(self);
        }

        private void Hook_Game_onDispose(Hook_Game.orig_onDispose orig, dc.pr.Game self)
        {
            orig(self);

            Logger.Information("Quiting...");

            client.Quit();
            Dispose();
        }

        private void Hook__File_saveSteamCloudStatus(HashlinkClosure orig)
        {

        }

        private bool? Hook__File_getSteamCloudStatus(HashlinkClosure orig)
        {
            return null;
        }

        private dc.haxe.io.Bytes Hook__File_getBytes(Hook__File.orig_getBytes orig, dc.String file)
        {
            var fn = file.ToString();
            if (fn.StartsWith("user"))
            {
                Debug.Assert(saveData != null);

                var bytes = dc.haxe.io.Bytes.Class.alloc(saveData.Length);
                saveData.CopyTo(bytes.AsSpan());
                return bytes;
            }
            return orig(file);
        }

        private void Hook__Save_save(Hook__Save.orig_save orig, dc.User u, bool onlyGameData)
        {

        }

        private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            if (e.Reason == DisconnectedReason.LocallyDisposed)
            {
                return;
            }
            Logger.Error(e.Exception, "Abort connection: {reason}: {desc}", e.Reason, e.Description);

            ModCore.Modules.Game.SynchronizationContext.Post(static _ =>
            {
                ClientMain.Instance.CleanupClient();

                Boot.Class.ME.returnToMainMenu();
            }, null);

            Dispose();
        }

        protected override void MyDispose()
        {
            base.MyDispose();

            prediction?.Clear();
            deathGate?.Clear();

            remoteWorld?.Dispose();
            remoteWorld = null;
            prediction = null;
            sendBuffer.Clear();

            serverStream?.Dispose();
            replicator?.Dispose();
            rpc?.Dispose();

            Hook_Mob.onDie -= Hook_Mob_onDie;
            Hook_Power.update -= Hook_Power_update;
            Hook__AttackUtils.applyHitResult -= Hook_AttackUtils_applyHitResult;
            Hook__AttackUtils.postHitResultUpdate -= Hook_AttackUtils_postHitResultUpdate;
            Hook__AttackUtils.createFromHeroAndHit -= Hook_AttackUtils_createFromHeroAndHit;
            Hook_LevelAreaAffect.init -= Hook_LevelAreaAffect_init;
            Hook_LevelAreaAffect.applyAnotherAffect -= Hook_LevelAreaAffect_applyAnotherAffect;
            localCapture?.Dispose();
            localCapture = null;

            Hook__Save.save -= Hook__Save_save;
            Hook__File.getBytes -= Hook__File_getBytes;

            foreach (var v in hooks)
            {
                v.Disable();
            }
            hooks.Clear();
        }

        /// <summary>
        /// 载入新 level 并初始化
        /// </summary>
        /// <param name="saveData"></param>
        /// <returns></returns>
        public async Task EnterNewLevel(byte[] saveData)
        {
            this.saveData = saveData;

            Logger.Information("Entering new level...");

            Main.Class.ME.options.disableLoreRooms = true;

            Main.Class.ME.cleanUser();
            Main.Class.ME.launchGame(new LaunchMode.LoadSave(), null, null);

            while (true)
            {
                await Task.Delay(1);
                DisposeToken.ThrowIfCancellationRequested();

                var g = dc.pr.Game.Class.ME;
                if (g?.curLevel == null || g.subLevels == null)
                {
                    continue;
                }
                if (!Main.Class.ME.isLoading)
                {
                    break;
                }
            }

            Logger.Information("Clearing entities...");

            var gm = dc.pr.Game.Class.ME;

            foreach (Level level in gm.subLevels)
            {
                List<Entity> entities = [];
                foreach (Entity v in level.entities)
                {
                    if (v is Hero || v is Interactive)
                    {
                        continue;
                    }
                    if (v is Mob)
                    {
                        entities.Add(v);
                    }

                }
                foreach (var v in entities)
                {
                    v.destroy();
                }
            }
            // #if true
            //             Debug.Assert(client.gameSessionInfo != null);
            //             var options = new JsonSerializerOptions { WriteIndented = true };
            //             Logger.Information("sever mobs  {info}\n", JsonSerializer.Serialize(client.gameSessionInfo, options));
            // #endif


            replicator?.Dispose();
            replicator = new(this);
            //replicator.Start();

            InitWorld();

            Debug.Assert(rpc != null);
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
            if (IsDisposed || uploadBroken) return;

            var game = dc.pr.Game.Class.ME;
            if (hero != game?.hero) return;
            if (hero.destroyed) return;

            // 每帧都采集最新状态;发送受背压控制,未发送的帧会被下一帧覆盖
            if (!sendBuffer.Capture(hero, Client.Guid, SyncedTimeMs)) return;

            if (uploadTask != null && !uploadTask.IsCompleted) return;

            var frame = sendBuffer.BuildFrameAndRecord();
            if (frame == null) return;

#if DEBUG
            if (WorldFrameDebug.ConsumeSkipClientUpload()) return;
#endif

            try
            {
                uploadTask = server.UploadClientWorldFrame(frame);
                _ = uploadTask.ContinueWith(OnUploadFaulted, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (Exception ex)
            {
                HandleUploadFailure(ex);
            }
        }

        private void OnUploadFaulted(Task task)
            => HandleUploadFailure(task.Exception?.GetBaseException());

        private void HandleUploadFailure(Exception? ex)
        {
            if (IsDisposed) return;

            // 连接已经断开时,等待 Rpc_Disconnected 清理
            if (ex is ConnectionLostException or OperationCanceledException)
            {
                uploadBroken = true;
                return;
            }

            long now = Environment.TickCount64;
            if (now - lastUploadErrorLogMs < 1000) return;

            lastUploadErrorLogMs = now;
            Logger.Warning("UploadClientWorldFrame failed: {Message}", ex?.Message ?? "unknown");
        }

        private void TickTimeSystem()
        {
            if (timeSystem.Ready || server == null) return;

            long now = ClientTimeSystem.NowMs();
            if (now - lastNtpRequestMs < NtpIntervalMs) return;
            if (ntpTask != null && !ntpTask.IsCompleted) return;

            lastNtpRequestMs = now;
            ntpTask = SampleNtpAsync();
        }

        private async Task SampleNtpAsync()
        {
            try
            {
                long t0 = ClientTimeSystem.NowMs();
                long serverTime = await Server.GetTimeStamp();
                long t1 = ClientTimeSystem.NowMs();

                timeSystem.AddSample(t0, t1, serverTime);
            }
            catch (Exception) when (IsDisposed) { }
            catch (Exception ex)
            {
                Logger.Error(ex, "NTP sample failed");
            }
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
#endif

            ObserveWorldEpoch(frame.Epoch);
            ObserveServerTime((long)frame.ServerTimeMs);
            remoteWorld?.OnWorldFrame(frame);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 服务端请求补发本地上传帧
        /// </summary>
        public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
            => Task.FromResult(sendBuffer.BuildBundle(request));

        internal void ObserveWorldEpoch(int epoch) => sendBuffer.SetEpoch(epoch);

        internal void ObserveServerTime(long serverTimeMs)
        {
            if (serverTimeMs <= lastServerFrameTimeMs) return;

            lastServerFrameTimeMs = serverTimeMs;
            timeSystem.OnSnapshot(serverTimeMs);
        }

        void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
        {
            sendBuffer.ResetLocalHeroState();
        }
    }
}
