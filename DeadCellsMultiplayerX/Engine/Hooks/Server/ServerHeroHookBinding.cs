namespace DeadCellsMultiplayerX.Engine.Hooks.Server;

using dc.en;
using DeadCellsMultiplayerX.Engine.Hooks.Shared;

/// <summary>
/// Hero Hook 的唯一订阅入口。
/// </summary>
internal static class ServerHeroHookBinding
{
    public static IDisposable Install(IServerHeroHookSink sink)
    {
        Hook_Hero.fixedUpdate += sink.OnFixedUpdate;
        Hook_Hero.postUpdate += sink.OnPostUpdate;
        Hook_Hero.onDamage += sink.OnDamage;
        Hook_Hero.canBeHitBy += sink.OnCanBeHitBy;
        Hook_Hero.initColorMap += sink.OnInitColorMap;

        var group = new HookGroup();
        group.Add(new HookLease(() => Hook_Hero.fixedUpdate -= sink.OnFixedUpdate));
        group.Add(new HookLease(() => Hook_Hero.postUpdate -= sink.OnPostUpdate));
        group.Add(new HookLease(() => Hook_Hero.onDamage -= sink.OnDamage));
        group.Add(new HookLease(() => Hook_Hero.canBeHitBy -= sink.OnCanBeHitBy));
        group.Add(new HookLease(() => Hook_Hero.initColorMap -= sink.OnInitColorMap));
        return group;
    }
}
