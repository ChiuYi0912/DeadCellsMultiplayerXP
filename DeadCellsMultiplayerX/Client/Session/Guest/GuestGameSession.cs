namespace DeadCellsMultiplayerX.Client.Session.Guest;

using System.Diagnostics.CodeAnalysis;
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
    // Fields
    private readonly Lock gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SynchronizationContext gameContext;
    private readonly Stream serverStream;
    private readonly Action<bool> requestStop;
    private readonly GuestGameLoader gameLoader;
    private readonly LocalActionCapture localCapture;
    private readonly TaskCompletionSource started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource initialWorld =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GuestLifetimeState state;
    private GuestRpcChannel? channel;
    private GuestSessionHooks? hooks;
    private GuestGameHookAdapter? gameHooks;
    private Task? loadTask;
    private dc.pr.Game? loadedGame;
    private byte[]? saveData;
    private bool eventsRegistered;
    private bool protocolValidated;
    private DebugHUD? debugHud;
    private volatile bool serverDrivenTransition;
    private int replayVisualDepth;

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
        this.gameLoader = new GuestGameLoader(gameContext, this.Logger);
        this.localCapture = new LocalActionCapture(this);

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
            this.localCapture.Commands.DrainPending,
            () => this.protocolValidated && !this.IsDisposed,
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

    public bool ServerDrivenTransition => this.serverDrivenTransition;
    public bool UploadBroken => this.Uploader.UploadBroken;
    public int LastWorldFrameBytes { get; private set; }
    public long SyncedTimeMs => this.Clock.SyncedTimeMs;
    public long ServerRttMs => this.Clock.RttMs;
    public long ServerClockOffsetMs => this.Clock.OffsetMs;

    // Internal properties
    internal ClientWorld? ClientWorld { get; private set; }
    internal CombatDeathGate DeathGate { get; } = new();
    internal int PendingCommands => this.localCapture.Commands.PendingCount;

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

    // Internal methods
    internal Task SubmitLevelVote(string levelId)
    {
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);

        return this.Server.SubmitLevelVote(new LevelVote
        {
            PlyerID = this.LocalGuid,
            NextLevelID = levelId,
            LevelSelected = true,
        });
    }

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
            this.TryCreateWorld();

            if (this.LobbyInfo.Owner == this.LocalGuid)
            {
                var path = Path.GetFullPath(
                    "save/" + dc.tool.Save.Class.fileName(null).ToString());

                var data = await File.ReadAllBytesAsync(path, this.lifetime.Token);
                this.lifetime.Token.ThrowIfCancellationRequested();
                await this.Server.UploadSavedata(data);
            }

            await this.initialWorld.Task.WaitAsync(this.lifetime.Token);

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

    private void InstallGameBindings()
    {
        this.gameHooks = new GuestGameHookAdapter(
            this.Logger,
            () => this.IsDisposed,
            () => this.serverDrivenTransition,
            this.SubmitLevelVote,
            () => this.requestStop(false));

        this.hooks = new GuestSessionHooks(
            new GuestCombatHookAdapter(
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                () => this.ClientWorld?.IsApplyingServerAreaAffect == true,
                this.PlayHitVisual),
            new GuestMobHookAdapter(
                () => this.ClientWorld?.MobModifiers,
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                mob => this.ClientWorld?.IsDeathSuppressed(mob) == true,
                () => this.SyncedTimeMs,
                this.DeathGate),
            new GuestSaveHookAdapter(() => this.saveData),
            this.gameHooks);

        this.localCapture.Init();
        this.eventsRegistered = true;
        EventSystem.AddReceiver(this);

#if DEBUG
        this.debugHud = DebugHUD.EnsureCreated();
#endif
    }
}
