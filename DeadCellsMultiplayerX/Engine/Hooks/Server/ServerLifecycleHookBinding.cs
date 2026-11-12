namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.h3d.mat;
using dc.hxd;
using dc.hxd.res;
using dc.pr;
using dc.tool;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;
using ModCore;
using ModCore.Modules;

/// <summary>
/// 进程级生命周期、资源与存档 Hook 的唯一订阅入口。
/// </summary>
internal static class ServerLifecycleHookBinding
{
    public static IDisposable Install(IServerLifecycleHookSink sink)
    {
        Hook_Window.setFullScreen += sink.OnSetFullScreen;
        Hook_Loader.loadCache += sink.OnLoadCache;
        Hook_Texture.dispose += sink.OnTextureDispose;
        dc.libs.heaps.slib.assets.Hook__Atlas.load += sink.OnAtlasLoad;
        Hook__Save.fileName += sink.OnSaveFileName;
        Hook_TitleScreen.initTitleScreen += sink.OnInitTitleScreen;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_Window.setFullScreen -= sink.OnSetFullScreen));
        group.Add(new HookLease(() => Hook_Loader.loadCache -= sink.OnLoadCache));
        group.Add(new HookLease(() => Hook_Texture.dispose -= sink.OnTextureDispose));
        group.Add(new HookLease(() => dc.libs.heaps.slib.assets.Hook__Atlas.load -= sink.OnAtlasLoad));
        group.Add(new HookLease(() => Hook__Save.fileName -= sink.OnSaveFileName));
        group.Add(new HookLease(() => Hook_TitleScreen.initTitleScreen -= sink.OnInitTitleScreen));

        if (GameInfo.Platform == GameInfo.PlatformKind.Steam)
        {
            group.Add(new RawHookLease(HashlinkHooks.Instance.CreateHook(
                "tool.$File", "getSteamCloudStatus", sink.OnGetSteamCloudStatus)));
            group.Add(new RawHookLease(HashlinkHooks.Instance.CreateHook(
                "tool.$File", "saveSteamCloudStatus", sink.OnSaveSteamCloudStatus)));
        }

        return group;
    }
}
