namespace DeadCellsMultiplayerX.Server.Integration.Hooks;

using dc.h3d.mat;
using dc.hl.types;
using dc.hxd;
using dc.hxd.res;
using dc.libs.heaps.slib;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Server;
using DeadCellsMultiplayerX.Server.Core;
using DeadCellsMultiplayerX.Server.Session;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using Serilog;

/// <summary>
/// 进程级生命周期、资源与存档 Hook 路由。
/// </summary>
internal sealed class ServerLifecycleHookRouter(ServerMain server) : IServerLifecycleHookSink
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "ServerLifecycleHookRouter");

    private readonly ServerMain server = server;
    private Sound? soundEmpty;

    /// <summary>
    /// 服务端窗口不需要全屏切换。
    /// </summary>
    public void OnSetFullScreen(Hook_Window.orig_setFullScreen orig, Window self, bool v)
    {
    }

    /// <summary>
    /// 减少不必要的资源加载。
    /// </summary>
    public Resource OnLoadCache(
        Hook_Loader.orig_loadCache orig,
        Loader self,
        dc.String path,
        dc.hl.Class c)
    {
        if (c == Sound.Class)
        {
            return this.soundEmpty ??= new Sound(
                self.load("sfx/noSound_Error.wav".AsHaxeString()).entry);
        }

        return orig(self, path, c);
    }

    /// <summary>
    /// 服务端不需要释放纹理。
    /// </summary>
    public void OnTextureDispose(Hook_Texture.orig_dispose orig, Texture self)
    {
    }

    /// <summary>
    /// 记录图集路径，供快照使用。
    /// </summary>
    public SpriteLib OnAtlasLoad(
        dc.libs.heaps.slib.assets.Hook__Atlas.orig_load orig,
        dc.String atlasPath,
        HlAction onReload,
        ArrayObj notZeroBaseds,
        ArrayObj properties)
    {
        var lib = orig(atlasPath, onReload, notZeroBaseds, properties);
        this.server.SpriteLibToAtlas[lib] = atlasPath.ToString();
        return lib;
    }

    /// <summary>
    /// 使用临时存档文件名，避免影响本地存档。
    /// </summary>
    public dc.String OnSaveFileName(Hook__Save.orig_fileName orig, int? slot) => global::System.IO.Path.GetFileName(this.server.SavePath).AsHaxeString();

    /// <summary>
    /// 首个 TitleScreen 初始化时创建服务端会话。
    /// </summary>
    public void OnInitTitleScreen(
        Hook_TitleScreen.orig_initTitleScreen orig,
        TitleScreen self,
        SpriteLib titleLib,
        Ref<int> bgType)
    {
        orig(self, titleLib, bgType);

        if (this.server.Session != null)
        {
            return;
        }

        var session = new ServerSession();
        this.server.AttachSession(session);
        _ = session.Init().ContinueWith(
            t =>
            {
                if (t.Exception?.InnerException is OperationCanceledException)
                {
                    return;
                }

                Logger.Error(t.Exception, "Server session init failed");
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    public bool? OnGetSteamCloudStatus(HashlinkClosure orig) => null;

    public void OnSaveSteamCloudStatus(HashlinkClosure orig)
    {
    }
}
