namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.h3d.mat;
using dc.hl.types;
using dc.hxd;
using dc.hxd.res;
using dc.libs.heaps.slib;
using dc.pr;
using dc.tool;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;

/// <summary>
/// 进程级生命周期、资源与存档 Hook 的端侧接收者。
/// </summary>
internal interface IServerLifecycleHookSink
{
    public void OnSetFullScreen(Hook_Window.orig_setFullScreen orig, Window self, bool v);

    public Resource OnLoadCache(
        Hook_Loader.orig_loadCache orig,
        Loader self,
        dc.String path,
        dc.hl.Class c);

    public void OnTextureDispose(Hook_Texture.orig_dispose orig, Texture self);

    public SpriteLib OnAtlasLoad(
        dc.libs.heaps.slib.assets.Hook__Atlas.orig_load orig,
        dc.String atlasPath,
        HlAction onReload,
        ArrayObj notZeroBaseds,
        ArrayObj properties);

    public dc.String OnSaveFileName(Hook__Save.orig_fileName orig, int? slot);

    public void OnInitTitleScreen(
        Hook_TitleScreen.orig_initTitleScreen orig,
        TitleScreen self,
        SpriteLib titleLib,
        Ref<int> bgType);

    public bool? OnGetSteamCloudStatus(HashlinkClosure orig);

    public void OnSaveSteamCloudStatus(HashlinkClosure orig);
}
