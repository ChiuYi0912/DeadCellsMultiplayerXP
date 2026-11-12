namespace DeadCellsMultiplayerX.Engine.Hooks.Client;

using dc.en;
using dc.pr;
using dc.shader;
using Hashlink.Virtuals;

/// <summary>
/// 客户端进程级 UI/资源 Hook 的端侧接收者。
/// </summary>
internal interface IClientLifecycleHookSink
{
    public void OnHeroPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self);

    public void OnTitleScreenDispose(Hook_TitleScreen.orig_onDispose orig, TitleScreen self);

    public void OnGlowKeyApply(
        Hook_GlowKey.orig_applyGlowData orig,
        GlowKey self,
        int i,
        virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData);

    public void OnTitleScreenMainMenu(Hook_TitleScreen.orig_mainMenu orig, TitleScreen self);

    public void OnTitleScreenCtor(
        Hook__TitleScreen.orig___constructor__ orig,
        TitleScreen arg1,
        bool? playMusic);
}
