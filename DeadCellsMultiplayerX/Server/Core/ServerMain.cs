namespace DeadCellsMultiplayerX.Server.Core;

using dc;
using DeadCellsMultiplayerX.Server.Integration.Hooks;
using DeadCellsMultiplayerX.Server.Session;
using Microsoft.VisualStudio.Threading;
using ModCore;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Serilog;

internal sealed class ServerMain : Module<ServerMain>,
    IOnBeforeGameInit,
    IOnGameExit
{
    /// <summary>
    /// 服务器入口点
    /// </summary>
    public static void Entrypoint()
    {
        _ = new ServerMain();

        // 启动游戏
        System.Type.GetType("ModCore.Startup, ModCore", true)!
            .GetMethod("StartGame")!
            .CreateDelegate<Func<int>>()
            ();
    }

    void IOnBeforeGameInit.OnBeforeGameInit() => this.Init();

    public Thread? MainThread { get; private set; }

    private readonly string savePath = Path.GetTempFileName();
    private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "ServerMain");
    private ServerHookBootstrap? hookBootstrap;
    private Task? sessionTask;
    private Task? shutdownTask;

    internal ServerSession? Session { get; private set; }
    internal bool IsStopping { get; private set; }

    /// <summary>
    /// 临时存档路径。
    /// </summary>
    internal string SavePath => this.savePath;

    /// <summary>
    /// 图集路径映射。
    /// </summary>
    internal Dictionary<object, string> SpriteLibToAtlas { get; } = [];

    /// <summary>
    /// 在 TitleScreen 创建会话后挂载。
    /// </summary>
    internal void AttachSession(ServerSession newSession)
    {
        newSession.VerifyGameThread();
        if (this.IsStopping || this.Session != null)
        {
            throw new InvalidOperationException("Server cannot attach another session.");
        }
        this.Session = newSession;
        this.sessionTask = this.ObserveSessionAsync(newSession);
    }

    private async Task ObserveSessionAsync(ServerSession session)
    {
        try
        {
            await session.Init();
        }
        catch (OperationCanceledException) when (session.IsDisposed) { }
        catch (Exception ex)
        {
            Log.Error(ex, "Server session init failed");
            session.Dispose();
        }

        // Keep the session reachable by hooks until native and transport cleanup finishes.
        await session.Completion;
        await session.GameContext;
        if (ReferenceEquals(this.Session, session)) this.Session = null;
    }

    /// <summary>
    /// 初始化服务器
    /// </summary>
    internal void Init()
    {
        this.MainThread = Thread.CurrentThread;

        // Hook 必须在读取配置前就位，保持与原 ServerMain.Init 的顺序一致。
        this.hookBootstrap = new ServerHookBootstrap(this);
        this.hookBootstrap.Install();

        // 避免影响本地配置
        var options = Options.Class.load();
        dc.tool.File.Class.PATH = Path.GetDirectoryName(this.SavePath)!.AsHaxeString();
        Options.Class.FILE = Path.GetFileName(Path.GetTempFileName()).AsHaxeString();

        options.skipCinematics = true; // 跳过动画
        options.disableLoreRooms = true; // 关闭剧情房

        options.save();

        AppDomain.CurrentDomain.ProcessExit += this.OnProcessExit;

        Log.Information("Server hooks installed.");
    }

    void IOnGameExit.OnGameExit() => this.DisposeSession();

    private void OnProcessExit(object? sender, EventArgs e)
    {
        if (Thread.CurrentThread == this.MainThread)
        {
            this.DisposeSession();
            return;
        }

        // Queue session and hook teardown together, so hooks remain installed until
        // native cleanup finishes. Never wait here: the game loop may have stopped.
        this.Session?.GameContext.Post(_ => this.DisposeSession(), null);
    }

    /// <summary>
    /// The server worker is a process-scoped singleton.  Explicit teardown is
    /// still required for a normal game exit (IOnGameExit / ProcessExit); a
    /// host-side Process.Kill() is the process-level fallback.
    /// </summary>
    private void DisposeSession()
    {
        if (Thread.CurrentThread != this.MainThread)
        {
            throw new InvalidOperationException("Server native teardown must run on the game thread.");
        }
        if (this.shutdownTask != null) return;
        this.IsStopping = true;
        this.shutdownTask = this.FinishShutdownAsync();
    }

    private async Task FinishShutdownAsync()
    {
        var session = this.Session;
        if (session != null)
        {
            await session.DisposeAsync();
            if (this.sessionTask != null) await this.sessionTask;
            await session.GameContext;
        }
        try
        {
            this.hookBootstrap?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Server hook dispose failed");
        }
        finally
        {
            this.hookBootstrap = null;
            AppDomain.CurrentDomain.ProcessExit -= this.OnProcessExit;
        }
    }
}
