namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc.cine;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using DeadCellsMultiplayerX.Client.World;
using DeadCellsMultiplayerX.Client.World.Heroes.Local;
using DeadCellsMultiplayerX.Contracts.Actions;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.SourceGenerator;
using StreamJsonRpc;

/// <summary>
/// 访客的客户端会话
/// </summary>
internal sealed partial class GuestClientSession(GuestClient client, Stream serverStream) : ClientSession,
    IGuestRPC,
    IOnFrameUpdate,
    IOnLocalHeroPostUpdate,
    IOnLocalHeroInitDone,
    IHeroActionSink
{
    /// <summary>
    /// 仅用于统计世界帧序列化大小
    /// </summary>
    private static readonly MessagePackSerializer FrameSizeSerializer = new();

    private JsonRpc? rpc;
    private IServerRPC server = null!;
    private byte[]? saveData;
    private GuestHookSession? hookSession;

    private LocalActionCapture? localCapture;
    private LocalCombatPrediction? prediction;
    private CombatDeathGate? deathGate;

    private volatile bool serverDrivenTransition;

    /// <summary>
    /// 受击表现调用深度 防止 Mob.onDamage 内部再次触发命中时递归
    /// </summary>
    private int replayVisualDepth;

    public static dc.pr.Game Game => dc.pr.Game.Class.ME;

    public IServerRPC Server => this.server ?? throw new InvalidOperationException();

    public GuestClient Client { get; private set; } = null!;

    /// <summary>
    /// 上传通道是否已判定为断开
    /// </summary>
    public bool UploadBroken => this.Uploader.UploadBroken;

    public bool ServerDrivenTransition
    {
        get => this.serverDrivenTransition;
        set => this.serverDrivenTransition = value;
    }

    /// <summary>
    /// 最近一帧世界帧的 MessagePack 负载字节数
    /// </summary>
    public int LastWorldFrameBytes { get; private set; }

    internal ClientWorld? ClientWorld { get; private set; }

    /// <summary>
    /// 本地武器命中预测
    /// </summary>
    internal LocalCombatPrediction Prediction
        => this.prediction ?? throw new InvalidOperationException();

    internal CombatDeathGate DeathGate
        => this.deathGate ?? throw new InvalidOperationException();

    private bool IsReady => this.server != null;

    private GuestFrameUploader Uploader => field ??= new GuestFrameUploader(
        this.Client.Guid,
        () => this.SyncedTimeMs,
        frame => this.server?.UploadClientWorldFrame(frame) ?? Task.CompletedTask,
        this.Logger);

    private GuestWorldLoader WorldLoader => field ??= new GuestWorldLoader(
        this.DisposeToken,
        this.ReplaceWorld,
        this.Uploader.ResetLocalHeroState,
        data => this.saveData = data,
        client,
        this.Logger);

    public override async Task Init()
    {
        this.deathGate = new CombatDeathGate();
        this.prediction = new LocalCombatPrediction(this);
        this.localCapture = new LocalActionCapture(this, this);
        this.localCapture.Init();

        this.InitHooks(this.deathGate);

#if DEBUG
        DebugHUD.EnsureCreated();
#endif

        var handshake = await GuestHandshake.RunAsync(
            this,
            serverStream,
            client,
            this.Rpc_Disconnected,
            this.Logger);

        if (handshake == null)
        {
            this.Dispose();
            return;
        }

        this.rpc = handshake.Rpc;
        this.server = handshake.Server;
        this.Client = client;

        this.Uploader.SetReady(true);
    }

    /// <summary>
    /// 载入新 level 并初始化
    /// </summary>
    /// <param name="saveData"></param>
    /// <returns></returns>
    public Task EnterNewLevel(byte[] saveData)
        => this.WorldLoader.EnterNewLevel(saveData);

    /// <summary>
    /// 服务端调用,用于通知当前客户切换关卡
    /// </summary>
    /// <param name="levelid"></param>
    /// <returns></returns>
    public Task EnterNextLevel(string levelid)
    {
        this.Logger.Information("Entering next level: {LevelId}", levelid);
        this.serverDrivenTransition = true;
        try
        {
            LevelTransition.Class.@goto.Invoke(levelid.AsHaxeString());
        }
        finally
        {
            this.serverDrivenTransition = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务端每 tick 推送完整世界帧
    /// </summary>
    public Task SyncWorldFrame(WorldFrame frame)
    {
        if (frame == null || !this.IsReady)
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

        this.ObserveWorldEpoch(frame.Epoch);
        this.ObserveServerTime((long)frame.ServerTimeMs);
        this.ClientWorld?.OnWorldFrame(frame);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务端请求补发本地上传帧
    /// </summary>
    public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
        => Task.FromResult(this.Uploader.BuildBundle(request));

    internal void ObserveWorldEpoch(int epoch) => this.Uploader.SetEpoch(epoch);

    internal void ObserveServerTime(long serverTimeMs) => this.Clock.ObserveServerTime(serverTimeMs);

    /// <summary>
    /// 播放远端 mob 的受击表现，避免 Mob.onDamage 内部递归
    /// </summary>
    internal void PlayHitVisual(AttackData atk, Mob mob)
    {
        if (mob == null || mob.destroyed || this.replayVisualDepth != 0)
        {
            return;
        }

        this.Logger.Verbose(
            "Puppet hit visual: target={Target} dmg={Dmg} hit={Hit}",
            mob.type, atk.finalDmg, atk.hitResult);

        this.replayVisualDepth++;
        try
        {
            mob.onDamage(atk);
        }
        finally
        {
            this.replayVisualDepth--;
        }
    }

    /// <summary>
    /// 强制客户端 mob 执行原生死亡
    /// </summary>
    internal void ForceNativeDeath(Mob mob)
    {
        if (mob == null || mob.destroyed)
        {
            return;
        }

        using (this.DeathGate.Allow(mob))
        {
            mob.life = 0;
            mob.onDie();
        }

        if (!mob.destroyed)
        {
            mob.destroy();
        }
    }

    internal void DebugCastSkill(string itemId, int slot)
        => this.localCapture?.DebugCastSkill(itemId, slot);

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        => this.localCapture?.DebugUseWeapon(itemId, slot, cycle);

    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        if (this.server == null)
        {
            return;
        }

        // 同步 TimeStamp
        this.TickTimeSystem();

        // 世界帧 NACK 调度/超时
        this.ClientWorld?.Tick(this.SyncedTimeMs);
    }

    void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero) => this.Uploader.CaptureAndUpload(hero);

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero) => this.Uploader.ResetLocalHeroState();

    Task IHeroActionSink.SubmitHeroAction(HeroAction action) => this.server.SubmitHeroAction(action);

    private ClientWorld ReplaceWorld()
    {
        this.prediction?.Clear();
        this.deathGate?.Clear();
        this.ClientWorld?.Dispose();
        this.ClientWorld = new ClientWorld(this, client.Guid);
        return this.ClientWorld;
    }

    private void TickTimeSystem() => this.Clock.Tick();

    /// <summary>
    /// 统计世界帧的 MessagePack 负载大小
    /// </summary>
    private void MeasureWorldFrame(WorldFrame frame)
    {
        var shape = TypeShapeProvider_DeadCellsMultiplayerX.Default
            .GetTypeShapeOrThrow<WorldFrame>();

        this.LastWorldFrameBytes = FrameSizeSerializer
            .Serialize(frame, shape)
            .Length;
    }

    /// <summary>
    /// 创建会话级 Hook 适配器；适配器只拿到完成工作所需的窄接口。
    /// </summary>
    private void InitHooks(CombatDeathGate deathGate)
    {
        var sink = new GuestSessionHookSink(
            new GuestCombatHookAdapter(
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                () => this.ClientWorld?.IsApplyingServerAreaAffect == true,
                this.PlayHitVisual),
            new GuestMobHookAdapter(
                () => this.ClientWorld?.MobModifiers,
                mob => this.ClientWorld?.IsRemoteMob(mob) == true,
                mob => this.ClientWorld?.IsDeathSuppressed(mob) == true,
                () => this.SyncedTimeMs,
                deathGate),
            new GuestSaveHookAdapter(
                () => this.saveData),
            new GuestGameHookAdapter(
                this.Logger,
                () => ClientRuntime.Instance.CurrentGuestClient,
                client.Quit,
                this.Dispose));

        this.hookSession = new GuestHookSession(sink);
    }
}
