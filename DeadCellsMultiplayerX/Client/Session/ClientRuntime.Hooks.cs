namespace DeadCellsMultiplayerX.Client.Session;

using dc.en;
using dc.pr;
using dc.shader;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using Hashlink.Virtuals;

/// <summary>
/// 进程级 Hook 的接口转发；处理逻辑保持在 ClientRuntime 原有方法中。
/// </summary>
internal sealed partial class ClientRuntime
{
    void IClientLifecycleHookSink.OnHeroPostUpdate(
        Hook_Hero.orig_postUpdate orig,
        Hero self)
        => Hook_Hero_postUpdate(orig, self);

    void IClientLifecycleHookSink.OnTitleScreenDispose(
        Hook_TitleScreen.orig_onDispose orig,
        TitleScreen self)
        => Hook_TitleScreen_onDispose(orig, self);

    void IClientLifecycleHookSink.OnGlowKeyApply(
        Hook_GlowKey.orig_applyGlowData orig,
        GlowKey self,
        int i,
        virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData)
        => Hook_GlowKey_applyGlowData(orig, self, i, glowData);

    void IClientLifecycleHookSink.OnTitleScreenMainMenu(
        Hook_TitleScreen.orig_mainMenu orig,
        TitleScreen self)
        => this.Hook_TitleScreen_mainMenu(orig, self);

    void IClientLifecycleHookSink.OnTitleScreenCtor(
        Hook__TitleScreen.orig___constructor__ orig,
        TitleScreen arg1,
        bool? playMusic)
        => Hook__TitleScreen__constructor__(orig, arg1, playMusic);
}
