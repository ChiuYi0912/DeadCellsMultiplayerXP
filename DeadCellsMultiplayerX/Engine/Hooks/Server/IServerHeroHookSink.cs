namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc;
using dc.en;
using dc.tool.atk;

/// <summary>
/// Hero 级 Hook 的端侧接收者。
/// </summary>
internal interface IServerHeroHookSink
{
    public void OnFixedUpdate(Hook_Hero.orig_fixedUpdate orig, Hero self);

    public void OnPostUpdate(Hook_Hero.orig_postUpdate orig, Hero self);

    public void OnDamage(Hook_Hero.orig_onDamage orig, Hero self, AttackData atk);

    public bool OnCanBeHitBy(Hook_Hero.orig_canBeHitBy orig, Hero self, Entity by);

    public void OnInitColorMap(Hook_Hero.orig_initColorMap orig, Hero self);
}
