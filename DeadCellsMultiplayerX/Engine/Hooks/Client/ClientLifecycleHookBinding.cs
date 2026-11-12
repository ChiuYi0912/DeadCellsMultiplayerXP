namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc.en;
using dc.pr;
using dc.shader;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// 客户端进程级 UI/资源 Hook 的唯一订阅入口。
/// </summary>
internal static class ClientLifecycleHookBinding
{
    public static IDisposable Install(IClientLifecycleHookSink sink)
    {
        Hook_Hero.postUpdate += sink.OnHeroPostUpdate;
        Hook_TitleScreen.onDispose += sink.OnTitleScreenDispose;
        Hook_GlowKey.applyGlowData += sink.OnGlowKeyApply;
        Hook_TitleScreen.mainMenu += sink.OnTitleScreenMainMenu;
        Hook__TitleScreen.__constructor__ += sink.OnTitleScreenCtor;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_Hero.postUpdate -= sink.OnHeroPostUpdate));
        group.Add(new HookLease(() => Hook_TitleScreen.onDispose -= sink.OnTitleScreenDispose));
        group.Add(new HookLease(() => Hook_GlowKey.applyGlowData -= sink.OnGlowKeyApply));
        group.Add(new HookLease(() => Hook_TitleScreen.mainMenu -= sink.OnTitleScreenMainMenu));
        group.Add(new HookLease(() => Hook__TitleScreen.__constructor__ -= sink.OnTitleScreenCtor));
        return group;
    }
}
