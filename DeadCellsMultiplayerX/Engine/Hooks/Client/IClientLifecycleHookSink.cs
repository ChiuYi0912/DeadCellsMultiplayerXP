using dc.en;
using dc.pr;
using Hashlink.Virtuals;

using dc.shader;
namespace DeadCellsMultiplayerX.Engine.Hooks.Client
{
    /// <summary>
    /// 客户端进程级 UI/资源 Hook 的端侧接收者。
    /// </summary>
    internal interface IClientLifecycleHookSink
    {
        void OnHeroPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self);

        void OnTitleScreenDispose(Hook_TitleScreen.orig_onDispose orig, TitleScreen self);

        void OnGlowKeyApply(
            Hook_GlowKey.orig_applyGlowData orig,
            GlowKey self,
            int i,
            virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData);

        void OnTitleScreenMainMenu(Hook_TitleScreen.orig_mainMenu orig, TitleScreen self);

        void OnTitleScreenCtor(
            Hook__TitleScreen.orig___constructor__ orig,
            TitleScreen arg1,
            bool? playMusic);
    }
}
