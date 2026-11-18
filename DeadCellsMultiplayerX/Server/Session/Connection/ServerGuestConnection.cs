namespace DeadCellsMultiplayerX.Server.Session.Connection;

using System.Diagnostics;
using dc.en;
using DeadCellsMultiplayerX.Contracts.Lobby;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using DeadCellsMultiplayerX.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Lifecycle;
using DeadCellsMultiplayerX.Networking.Rpc.Contracts;
using DeadCellsMultiplayerX.Networking.Transport;
using DeadCellsMultiplayerX.Server.Integration.Events;
using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using ModCore.Modules;
using Serilog;
using StreamJsonRpc;

internal sealed partial class ServerGuestConnection :
    DisposableEventReceiver,
    IOnServerEnterNewLevel
{


    private readonly JsonRpc rpc;
    public override ILogger Logger { get; }

    /// <summary>
    /// 连接层身份。来自握手 GUID，不随后续客户端消息变化。
    /// </summary>
    public string ConnectionGuid { get; }

    public GuestInfo GuestInfo { get; set; } = new();
    public ServerSession Session { get; }
    public ServerMainThread Main => this.Session.Main;
    public IGuestRPC Guest { get; private set; } = null!;

    public ServerGuestConnection(ServerSession session, Stream connection, string connectionGuid)
    {
        session.VerifyGameThread();
        this.Session = session;
        this.ConnectionGuid = connectionGuid
            ?? throw new ArgumentNullException(nameof(connectionGuid));
        this.Logger = Log.ForContext("SourceContext", "Server-Guest-" + this.ConnectionGuid);

        this.rpc = connection.CreateJsonRpc("server-guest", NetLogSession.CreateJsonRpcFailureListener("server-guest"));

        this.rpc.AddLocalRpcTarget(this);


        this.Guest = this.rpc.Attach<IGuestRPC>();

        this.rpc.SynchronizationContext = session.GameContext;

        this.rpc.Disconnected += this.Rpc_Disconnected;

        this.rpc.StartListening();

        this.Logger.Information("Connected.");
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

    protected override void MyDispose()
    {
        if (this.Session.IsGameThread)
        {
            this.DisposeOnGameThread();
            return;
        }

        _ = this.DisposeOnGameThreadAsync().ContinueWith(
            task => this.Logger.Error(task.Exception, "Guest connection dispose failed"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    protected override ValueTask MyDisposeAsync() => new(this.DisposeOnGameThreadAsync());

    private async Task DisposeOnGameThreadAsync()
    {
        await this.Session.GameContext;
        this.DisposeOnGameThread();
    }

    private void DisposeOnGameThread()
    {
        this.Session.VerifyGameThread();
        this.Session.RemoveGuest(this);

        base.MyDispose();

        this.rpc?.Dispose();
    }

    /// <summary>广播一帧世界快照(不阻塞游戏线程)。</summary>
    public void PushWorldFrame(WorldFrame frame)
    {
        if (this.IsDisposed || this.rpc.IsDisposed)
        {
            return;
        }

        try
        {
            var task = this.Guest.SyncWorldFrame(frame);
            _ = task.ContinueWith(
                t => this.Logger.Warning(t.Exception, "SyncWorldFrame failed"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
        catch (Exception ex)
        {
            // 同步序列化/连接异常不能冒泡到广播循环,否则会打断整个服务端 tick
            this.Logger.Warning(ex, "SyncWorldFrame failed (sync)");
        }
    }

    /// <summary>向该客户端请求补发上传帧。</summary>
    public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
        => this.Guest.RequestClientFrames(request);

    void IOnServerEnterNewLevel.OnServerEnterNewLevel()
    {
        if (this.IsDisposed || this.Session.IsDisposed)
        {
            return;
        }
        this.Session.VerifyGameThread();
        Debug.Assert(this.Main.SavePath != null);

        this.Guest.EnterNewLevel(File.ReadAllBytes(this.Main.SavePath));

        EventSystem.BroadcastEvent<IOnHeroInitDone, Hero>(Game.Instance.HeroInstance!);
    }



}
