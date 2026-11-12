using dc.en;
using dc.tool.atk;

using dc;
namespace DeadCellsMultiplayerX.Engine.Hooks.Server
{
    /// <summary>
    /// Hero 级 Hook 的端侧接收者。
    /// </summary>
    internal interface IServerHeroHookSink
    {
        void OnFixedUpdate(Hook_Hero.orig_fixedUpdate orig, Hero self);

        void OnPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self);

        void OnDamage(Hook_Hero.orig_onDamage orig, Hero self, AttackData atk);

        bool OnCanBeHitBy(Hook_Hero.orig_canBeHitBy orig, Hero self, Entity by);

        void OnInitColorMap(Hook_Hero.orig_initColorMap orig, Hero self);
    }
}
