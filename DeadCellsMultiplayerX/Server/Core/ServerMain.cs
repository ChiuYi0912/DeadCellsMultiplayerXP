using dc;
using DeadCellsMultiplayerX.Server.Integration.Hooks;
using DeadCellsMultiplayerX.Server.Session;
using ModCore;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Serilog;

namespace DeadCellsMultiplayerX.Server.Core
{
    internal class ServerMain : Module<ServerMain>,
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

        void IOnBeforeGameInit.OnBeforeGameInit()
        {
            Init();
        }

        public Thread? MainThread { get; private set; }

        public readonly string savePath = System.IO.Path.GetTempFileName();
        private readonly Dictionary<object, string> spriteLibToAtlas = [];

        private static readonly ILogger Log = Serilog.Log.ForContext("SourceContext", "ServerMain");

        private ServerSession? session;
        private ServerHookBootstrap? hookBootstrap;

        internal ServerSession? Session => session;

        /// <summary>
        /// 临时存档路径。
        /// </summary>
        internal string SavePath => savePath;

        /// <summary>
        /// 图集路径映射。
        /// </summary>
        internal Dictionary<object, string> SpriteLibToAtlas => spriteLibToAtlas;

        /// <summary>
        /// 在 TitleScreen 创建会话后挂载。
        /// </summary>
        internal void AttachSession(ServerSession newSession)
        {
            session = newSession;
        }

        /// <summary>
        /// 初始化服务器
        /// </summary>
        internal void Init()
        {
            MainThread = Thread.CurrentThread;

            // Hook 必须在读取配置前就位，保持与原 ServerMain.Init 的顺序一致。
            hookBootstrap = new ServerHookBootstrap(this);
            hookBootstrap.Install();

            // 避免影响本地配置
            var options = Options.Class.load();
            dc.tool.File.Class.PATH = System.IO.Path.GetDirectoryName(savePath)!.AsHaxeString();
            dc.Options.Class.FILE = System.IO.Path.GetFileName(System.IO.Path.GetTempFileName()).AsHaxeString();

            options.skipCinematics = true; // 跳过动画
            options.disableLoreRooms = true; // 关闭剧情房

            options.save();

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Log.Information("Server hooks installed.");
        }

        void IOnGameExit.OnGameExit()
        {
            DisposeSession();
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            DisposeSession();
        }

        /// <summary>
        /// The server worker is a process-scoped singleton.  Explicit teardown is
        /// still required for a normal game exit (IOnGameExit / ProcessExit); a
        /// host-side Process.Kill() is the process-level fallback.
        /// </summary>
        private void DisposeSession()
        {
            try
            {
                session?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Server session dispose failed");
            }
            finally
            {
                session = null;
            }

            try
            {
                hookBootstrap?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Server hook dispose failed");
            }
            finally
            {
                hookBootstrap = null;
            }
        }
    }
}
