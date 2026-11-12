using dc.h3d.mat;
using dc.hxd.res;
using dc.libs.heaps.slib;
using dc.pr;
using dc.hl.types;
using Hashlink.Proxy.Clousre;
using HaxeProxy.Runtime;

using dc.hxd;
using dc.tool;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// 进程级生命周期、资源与存档 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerLifecycleHookSink
    {
        void OnSetFullScreen(Hook_Window.orig_setFullScreen orig, Window self, bool v);

        Resource OnLoadCache(
            Hook_Loader.orig_loadCache orig,
            Loader self,
            dc.String path,
            dc.hl.Class c);

        void OnTextureDispose(Hook_Texture.orig_dispose orig, Texture self);

        SpriteLib OnAtlasLoad(
            dc.libs.heaps.slib.assets.Hook__Atlas.orig_load orig,
            dc.String atlasPath,
            HlAction onReload,
            ArrayObj notZeroBaseds,
            ArrayObj properties);

        dc.String OnSaveFileName(Hook__Save.orig_fileName orig, int? slot);

        void OnInitTitleScreen(
            Hook_TitleScreen.orig_initTitleScreen orig,
            TitleScreen self,
            SpriteLib titleLib,
            Ref<int> bgType);

        bool? OnGetSteamCloudStatus(HashlinkClosure orig);

        void OnSaveSteamCloudStatus(HashlinkClosure orig);
    }
}
