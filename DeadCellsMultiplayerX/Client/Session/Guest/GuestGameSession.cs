namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics.CodeAnalysis;
using dc.cine;
using dc.en;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Client.World;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Contracts.Protocol;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Foundation.Versioning;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.SourceGenerator;
using Serilog;

/// <summary>Owns one game session, including startup, inbound RPC and game resources.</summary>
internal sealed partial class GuestGameSession
    : IGuestRPC,
      IEventReceiver,
      IOnFrameUpdate,
      IOnLocalHeroPostUpdate,
      IOnLocalHeroInitDone,
      IDisposable,
      System.IAsyncDisposable
{
    private static readonly MessagePackSerializer FrameSizeSerializer = new();

    // Fields
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SynchronizationContext gameContext;
    private readonly Stream serverStream;
    private readonly Action<bool> requestStop;
    private readonly GuestGameLoader gameLoader;
    private readonly LocalHeroActionRecorder localActions;
    private readonly GuestLevelTransition levelLifecycle = new();
    private readonly TaskCompletionSource started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource initialWorldStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GuestLifetimeState state;
    private GuestRpcChannel? channel;
    private GuestSessionHooks? hooks;
    private GuestGameHookAdapter? gameHooks;
    private GuestLevelEntrySubscription? levelEntry;
    private Task? loadTask;
    private Task? levelVoteTask;
    private dc.pr.Game? loadedGame;
    private byte[]? saveData;
    private bool eventsRegistered;
    private TaskCompletionSource? creatingWorld;
    private TaskCompletionSource? releasingWorld;
    private long worldGeneration;
    private bool protocolValidated;
    private DebugHUD? debugHud;
    private volatile string? serverTransitionTarget;

    // Constructor
    public GuestGameSession(
        string localGuid,
        LobbyInfo lobbyInfo,
        Stream serverStream,
        SynchronizationContext gameContext,
        Action<bool> requestStop)
    {
        this.LocalGuid = localGuid;
        this.LobbyInfo = lobbyInfo;
        this.serverStream = serverStream;
        this.gameContext = gameContext;
        this.requestStop = requestStop;
        this.Logger = Log.ForContext<GuestGameSession>();
        this.Combat = new GuestCombatPresentation(this.Logger);
        this.gameLoader = new GuestGameLoader(gameContext, this.Logger);
        this.localActions = new LocalHeroActionRecorder(this);

        // Bind session methods here; they resolve Server only when invoked.
        // Binding Server.Method directly would access RPC before RunStartAsync.
        this.Clock = new GuestClockSync(
            () => this.IsDisposed,
            () => this.protocolValidated && !this.IsDisposed,
            this.RequestServerTimeAsync,
            this.Logger,
            gameContext);

        this.Uploader = new GuestFrameUploader(
            localGuid,
            () => this.SyncedTimeMs,
            this.UploadClientFrameAsync,
            this.localActions.Commands.GetPending,
            () => this.CanUploadFrames,
            this.Logger);
    }

    // Public properties
    public static dc.pr.Game Game => dc.pr.Game.Class.ME;

    public string LocalGuid { get; }
    public LobbyInfo LobbyInfo { get; }

    [AllowNull]
    public IServerRPC Server
    {
        get => field ?? throw new InvalidOperationException(
            "Guest RPC is not initialized.");
        private set;
    }

    public bool IsDisposed
    {
        get
        {
            lock (this.gate)
            {
                return this.state >= GuestLifetimeState.Stopping;
            }
        }
    }

    public GuestLifetimeState State
    {
        get
        {
            lock (this.gate)
            {
                return this.state;
            }
        }
    }

    public bool ServerDrivenTransition => this.serverTransitionTarget != null;
    public bool UploadBroken => this.Uploader.UploadBroken;
    public int LastWorldFrameBytes { get; private set; }
    public long SyncedTimeMs => this.Clock.SyncedTimeMs;
    public long ServerRttMs => this.Clock.RttMs;
    public long ServerClockOffsetMs => this.Clock.OffsetMs;

    // Internal properties
    internal ClientWorld? ClientWorld { get; private set; }
    internal GuestCombatPresentation Combat { get; }
    internal int PendingCommands => this.localActions.Commands.PendingCount;
    internal bool CanUploadFrames => this.protocolValidated && !this.IsDisposed
        && this.levelLifecycle.CanUploadFrames;
    internal bool CanRecordActions => !this.IsDisposed && this.levelLifecycle.CanRecordActions;
    internal bool CanTickWorld => !this.IsDisposed && this.levelLifecycle.CanTickWorld;

    internal bool CanApplyWorldEpoch(int epoch)
        => !this.IsDisposed && this.levelLifecycle.CanApplyWorldEpoch(epoch);

    internal bool CanRepairHeroTargetability(Hero hero)
        => !this.IsDisposed && ReferenceEquals(hero, Game?.hero)
            && this.levelLifecycle.Phase == GuestLevelPhase.Ready;

    // Private properties
    private ILogger Logger { get; }
    private GuestClockSync Clock { get; }
    private GuestFrameUploader Uploader { get; }

    // Public methods
    public Task StartAsync()
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(
                this.state >= GuestLifetimeState.Stopping,
                this);

            if (this.state != GuestLifetimeState.Created)
            {
                return this.started.Task;
            }

            this.state = GuestLifetimeState.Starting;
        }

        _ = this.RunStartAsync();
        return this.started.Task;
    }

    // Inbound RPC
    public Task EnterNewLevel(byte[] data)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.state >= GuestLifetimeState.Stopping, this);
            // A repeated callback shares the initial load; it cannot launch a second game.
            return this.loadTask ??= this.LoadGameAsync(data);
        }
    }

    /// <summary>
    /// 服务端调用,用于通知当前客户切换关卡
    /// </summary>
    /// <param name="levelid"></param>
    /// <returns></returns>
    public Task EnterNextLevel(string levelid)
    {
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        ArgumentException.ThrowIfNullOrEmpty(levelid);
        if (this.serverTransitionTarget != null
            || this.levelLifecycle.Phase is not (GuestLevelPhase.Ready or GuestLevelPhase.WaitingForServer))
        {
            this.Logger.Debug("Ignoring level command in {Phase}: {LevelId}", this.levelLifecycle.Phase, levelid);
            return Task.CompletedTask;
        }
        this.Logger.Information("Entering next level: {LevelId}", levelid);
        this.serverTransitionTarget = levelid;
        LevelTransition.Class.@goto.Invoke(levelid.AsHaxeString());
        this.serverTransitionTarget = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务端每 tick 推送完整世界帧
    /// </summary>
    public Task SyncWorldFrame(WorldFrame frame)
    {
        if (frame == null || !this.protocolValidated || this.IsDisposed)
        {
            return Task.CompletedTask;
        }

#if DEBUG
        if (WorldFrameDebug.ConsumeDropServerFrame())
        {
            return Task.CompletedTask;
        }

        if (DebugFlags.Get("netstat-Measure"))
        {
            this.MeasureWorldFrame(frame);
        }
#endif

        this.levelLifecycle.ObserveServerEpoch(frame.Epoch);
        this.ClientWorld?.OnWorldFrame(frame);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务端请求补发本地上传帧
    /// </summary>
    public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
        => Task.FromResult(!this.CanUploadFrames ? new ClientFrameBundle { Unavailable = true }
            : this.Uploader.BuildBundle(request));

    // Game events
    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        if (!this.protocolValidated || this.IsDisposed)
        {
            return;
        }

        // 同步 TimeStamp
        this.Clock.Tick();

        // 世界帧 NACK 调度/超时
        if (this.CanTickWorld)
        {
            this.ClientWorld?.Tick(this.SyncedTimeMs);
        }
    }

    void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
    {
        if (!this.IsDisposed)
        {
            this.Uploader.CaptureAndUpload(hero);
        }
    }

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        if (!this.IsDisposed)
        {
            this.localActions.OnHeroInitialized(hero);
            this.Uploader.ResetLocalHeroState();
        }
    }

    // Internal methods
    internal async Task SubmitLevelVote(string levelId)
    {
        await this.gameContext;
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        ArgumentException.ThrowIfNullOrEmpty(levelId);

        if (this.levelLifecycle.Phase == GuestLevelPhase.Ready)
        {
            if (!this.levelLifecycle.TryBeginWaitingForServer(levelId, this.ClientWorld?.Epoch ?? -1, out var generation))
            {
                return;
            }
            this.levelVoteTask = null;
            var game = Game;
            if (game?.hero != null)
            {
                game.controller.manualLock = true;
                var vp = game.curLevel.viewport;
                if (vp != null && !vp.destroyed)
                {
                    vp.stopTracking();
                }

                game.hero.clean();
            }
            this.Logger.Information("Guest waiting for level selection: generation={Generation} vote={LevelId}", generation, levelId);
        }
        else if (this.levelLifecycle.Phase != GuestLevelPhase.WaitingForServer
            || !string.Equals(levelId, this.levelLifecycle.TargetLevelId, StringComparison.Ordinal))
        {
            return;
        }

        // Retries share an in-flight vote; failure never implies the server did not commit it.
        if (this.levelVoteTask == null || (this.levelVoteTask.IsCompleted && !this.levelVoteTask.IsCompletedSuccessfully))
        {
            this.levelVoteTask = this.Server.SubmitLevelVote(new LevelVote
            {
                PlyerID = this.LocalGuid,
                NextLevelID = levelId,
                LevelSelected = true,
            });
        }
        await this.levelVoteTask;
    }

    private (long Generation, dc.pr.Game Game)? TryBeginNativeLevelTransition(LevelTransition transition, string levelId)
    {
        var game = Game;
        if (this.IsDisposed || game == null || !this.levelLifecycle.TryBeginNativeTransition(
            levelId, this.ClientWorld?.Epoch ?? -1, transition, out var generation))
        {
            return null;
        }
        this.Logger.Information("Guest native level loading: generation={Generation} target={LevelId}", generation, levelId);
        this.ReleaseWorld();
        if (this.IsDisposed || this.levelLifecycle.Generation != generation)
        {
            return null;
        }
        return (generation, game);
    }

    private void OnNativeLevelConstructed(long generation, LevelTransition transition, dc.pr.Game game, string levelId)
    {
        if (this.IsDisposed || this.levelLifecycle.Generation != generation || game != Game)
        {
            return;
        }
        // Phase 0 confirmed that native entry completes after the constructor returns.
        this.ClearLevelEntrySubscription();
        this.levelEntry = new GuestLevelEntrySubscription(transition,
            () => this.OnNativeLevelEntered(generation, transition, game, levelId));
    }

    private void OnNativeLevelEntered(long generation, LevelTransition transition, dc.pr.Game game, string levelId)
    {
        if (this.IsDisposed || generation != this.levelLifecycle.Generation || game != Game
            || !this.levelLifecycle.TryCompleteNativeLoad(generation, transition, levelId))
        {
            return;
        }
        this.ClearLevelEntrySubscription();
        this.Logger.Information("Guest native level entered: generation={Generation} target={LevelId}", generation, levelId);
        this.CreateWorld(game, this.levelLifecycle.MinimumEpochExclusive);
    }

    private void ClearLevelEntrySubscription()
    {
        var entry = this.levelEntry;
        this.levelEntry = null;
        this.Cleanup(() => entry?.Dispose(), "level entry subscription");
    }

    private void OnWorldBaselineApplied(ClientWorld world, int epoch)
    {
        if (this.IsDisposed || !ReferenceEquals(world, this.ClientWorld) || world.IsDisposed
            || !this.levelLifecycle.TryCompleteBaseline(this.worldGeneration, epoch))
        {
            return;
        }
        var generation = this.worldGeneration;
        if (!this.IsDisposed && this.levelLifecycle.Generation == generation
            && this.levelLifecycle.Phase == GuestLevelPhase.Ready)
        {
            this.Logger.Information("Guest level ready: generation={Generation} epoch={Epoch} level={LevelId}",
                generation, epoch, this.levelLifecycle.TargetLevelId);
        }
    }

    /// <summary>
    /// 仅由时间线交付的帧确认命令；普通推送与补发共用此入口。
    /// </summary>
    internal void AcknowledgeCommands(WorldFrame frame)
        => this.localActions?.Commands.Acknowledge(frame, this.LocalGuid);

    internal void ObserveWorldEpoch(int epoch)
    {
        if (this.IsDisposed || !this.levelLifecycle.TryAcceptWorldEpoch(this.levelLifecycle.Generation, epoch))
        {
            return;
        }
        this.Uploader.SetEpoch(epoch);
        this.localActions?.ObserveEpoch(epoch);
    }

    internal void ObserveServerTime(long serverTimeMs) => this.Clock.ObserveServerTime(serverTimeMs);

    internal void DebugCastSkill(string itemId, int slot)
        => this.localActions?.DebugCastSkill(itemId, slot);

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        => this.localActions?.DebugUseWeapon(itemId, slot, cycle);

    // Private methods
    private Task<long> RequestServerTimeAsync() => this.Server.GetTimeStamp();

    private Task UploadClientFrameAsync(ClientWorldFrame frame)
        => this.Server.UploadClientWorldFrame(frame);

    private async Task RunStartAsync()
    {
        try
        {
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();
            this.InstallGameBindings();

            // UploadSavedata can invoke EnterNewLevel before its response arrives.
            // Publish the RPC target and install save/game hooks before listening.
            this.channel = new GuestRpcChannel(
                this.serverStream,
                "guest-server",
                this.Logger);

            var rpc = this.channel.Rpc;
            rpc.AddLocalRpcTarget(this);
            this.Server = rpc.Attach<IServerRPC>();
            rpc.SynchronizationContext = this.gameContext;
            rpc.Disconnected += this.OnDisconnected;
            rpc.StartListening();
            this.lifetime.Token.ThrowIfCancellationRequested();

            var hello = await this.Server.Hello(new HelloRequest
            {
                MAJOR = ProtocolVersion.Major,
                MINOR = ProtocolVersion.Minor,
                ModVersion = VersionUtils.ModVersion.ToString(),
                Capabilities = ProtocolVersion.SupportedCapabilities,
            });

            this.lifetime.Token.ThrowIfCancellationRequested();

            if (!hello.Accepted)
            {
                throw new InvalidOperationException(
                    $"Guest handshake rejected: {hello.Reason}");
            }

            if (!await this.Server.CheckVersion(VersionUtils.ModVersion.ToString()))
            {
                throw new InvalidOperationException("Guest game version mismatch.");
            }

            this.lifetime.Token.ThrowIfCancellationRequested();
            await this.Server.SetGuestInfo(this.LobbyInfo.Guests[this.LocalGuid]);
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();

            this.protocolValidated = true;
            this.TryStartInitialWorld();

            if (this.LobbyInfo.Owner == this.LocalGuid)
            {
                var path = Path.GetFullPath(
                    "save/" + dc.tool.Save.Class.fileName(null).ToString());

                var data = await File.ReadAllBytesAsync(path, this.lifetime.Token);
                this.lifetime.Token.ThrowIfCancellationRequested();
                await this.Server.UploadSavedata(data);
            }

            await this.initialWorldStarted.Task.WaitAsync(this.lifetime.Token);

            lock (this.gate)
            {
                if (this.state >= GuestLifetimeState.Stopping)
                {
                    throw new OperationCanceledException(this.lifetime.Token);
                }

                this.state = GuestLifetimeState.Active;
            }

            this.started.TrySetResult();
        }
        catch (Exception ex)
        {
            if (this.lifetime.IsCancellationRequested || ex is OperationCanceledException)
            {
                this.started.TrySetCanceled(this.lifetime.Token);
            }
            else
            {
                this.started.TrySetException(ex);
            }

            if (!this.IsDisposed)
            {
                this.Logger.Error(ex, "Guest game initialization failed");
                this.requestStop(true);
            }
        }
    }

    private async Task LoadGameAsync(byte[] data)
    {
        try
        {
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();
            this.saveData = data;
            this.loadedGame = await this.gameLoader.LoadSavedGameAsync(this.lifetime.Token);
            this.TryStartInitialWorld();
        }
        catch (Exception ex)
        {
            this.initialWorldStarted.TrySetCanceled();
            if (!this.IsDisposed)
            {
                this.Logger.Error(ex, "Guest game loading failed");
                this.requestStop(true);
            }
            throw;
        }
    }

    private void InstallGameBindings()
    {
        this.gameHooks = new GuestGameHookAdapter(
            this.Logger,
            () => this.IsDisposed,
            () => this.levelLifecycle.Phase == GuestLevelPhase.InitialLoading,
            () => this.serverTransitionTarget,
            this.SubmitLevelVote,
            this.TryBeginNativeLevelTransition,
            this.OnNativeLevelConstructed,
            () => this.requestStop(false));

        this.hooks = new GuestSessionHooks(
            new GuestCombatHookAdapter(
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                () => this.ClientWorld?.IsApplyingServerAreaAffect == true,
                this.Combat),
            new GuestMobHookAdapter(
                () => this.ClientWorld?.MobModifiers,
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                mob => this.ClientWorld?.IsDeathSuppressed(mob) == true,
                () => this.SyncedTimeMs,
                this.Combat),
            new GuestSaveHookAdapter(() => this.saveData),
            this.gameHooks);

        this.localActions.Init();
        this.eventsRegistered = true;
        EventSystem.AddReceiver(this);

#if DEBUG
        this.debugHud = DebugHUD.EnsureCreated();
#endif
    }

    private void TryStartInitialWorld()
    {
        dc.pr.Game game;
        lock (this.gate)
        {
            if (this.state >= GuestLifetimeState.Stopping)
            {
                throw new OperationCanceledException(this.lifetime.Token);
            }

            if (!this.protocolValidated || this.loadedGame == null
                || this.ClientWorld != null || this.creatingWorld != null)
            {
                return;
            }
            game = this.loadedGame;
        }

        var levelId = game.curLevel?.map?.id?.ToString() ?? string.Empty;
        var generation = this.levelLifecycle.Generation;
        if (!this.levelLifecycle.TryCompleteInitialLoad(generation, levelId))
        {
            return;
        }
        this.CreateWorld(game);
        // Only the initial startup path completes this signal. A later world
        // replacement must not change startup completion or wait for a baseline.
        this.initialWorldStarted.TrySetResult();
    }

    private ClientWorld CreateWorld(dc.pr.Game game, int minimumEpochExclusive = -1)
    {
        CancellationToken cancellationToken;
        TaskCompletionSource creation;
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.state >= GuestLifetimeState.Stopping, this);
            if (this.ClientWorld != null || this.creatingWorld != null)
            {
                throw new InvalidOperationException("A guest world already exists or is being created.");
            }
            cancellationToken = this.lifetime.Token;
            creation = this.creatingWorld = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        ClientWorld? world = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (game != Game)
            {
                throw new InvalidOperationException("Guest game was replaced during initialization.");
            }
            this.Combat.ClearDeathPermissions();
            // Native component construction may reenter game code. Do not hold
            // the session lock, and do not start network delivery in the constructor.
            world = new ClientWorld(this, this.LocalGuid, minimumEpochExclusive);
            cancellationToken.ThrowIfCancellationRequested();
            if (game != Game)
            {
                throw new InvalidOperationException("Guest game was replaced during initialization.");
            }
            lock (this.gate)
            {
                ObjectDisposedException.ThrowIf(this.state >= GuestLifetimeState.Stopping, this);
                this.ClientWorld = world;
                this.worldGeneration = this.levelLifecycle.Generation;
            }
            world.BaselineApplied += this.OnWorldBaselineApplied;
            this.Uploader.ResetLocalHeroState();
            cancellationToken.ThrowIfCancellationRequested();
            world.Start();
            cancellationToken.ThrowIfCancellationRequested();
            if (world.IsDisposed || !ReferenceEquals(this.ClientWorld, world))
            {
                throw new OperationCanceledException("Guest world was released during startup.", cancellationToken);
            }
            return world;
        }
        catch (Exception ex)
        {
            if (world != null)
            {
                if (ReferenceEquals(this.ClientWorld, world))
                {
                    this.Cleanup(this.ReleaseWorld, "world startup rollback");
                }
                else
                {
                    this.Cleanup(world.Dispose, "unpublished world");
                }
            }
            if (!this.IsDisposed)
            {
                this.Logger.Error(ex, "Guest world creation failed");
                this.Dispose();
                this.requestStop(true);
            }
            throw;
        }
        finally
        {
            lock (this.gate)
            {
                this.creatingWorld = null;
            }
            creation.TrySetResult();
        }
    }

    private void MeasureWorldFrame(WorldFrame frame)
    {
        var shape = TypeShapeProvider_DeadCellsMultiplayerX.Default
            .GetTypeShapeOrThrow<WorldFrame>();

        this.LastWorldFrameBytes = FrameSizeSerializer
            .Serialize(frame, shape)
            .Length;
    }
}
