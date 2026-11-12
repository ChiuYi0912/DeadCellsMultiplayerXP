namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using dc;
using dc.en;
using dc.hl.types;
using dc.tool;
using dc.tool.atk;
using dc.tool.weap;
using DeadCellsMultiplayerX.Client.World.Heroes.Remote;
using DeadCellsMultiplayerX.Engine.Adapters.Heroes;

/// <summary>
/// 会话战斗 Hook：只处理命中表现、区域效果门控、盾反手雷与 Power 清理。
/// 世界通过委托按需查询，适配器不持有会话。
/// </summary>
internal sealed class GuestCombatHookAdapter(
    Func<Mob, bool> isRemoteMob,
    Func<bool> isApplyingServerAreaAffect,
    Action<AttackData, Mob> playHitVisual)
{
    private readonly Func<Mob, bool> isRemoteMob = isRemoteMob
            ?? throw new ArgumentNullException(nameof(isRemoteMob));
    private readonly Func<bool> isApplyingServerAreaAffect = isApplyingServerAreaAffect
            ?? throw new ArgumentNullException(nameof(isApplyingServerAreaAffect));
    private readonly Action<AttackData, Mob> playHitVisual = playHitVisual
            ?? throw new ArgumentNullException(nameof(playHitVisual));

    public static void CounterGrenade(
        Hook_BaseShield.orig_counterGrenade orig,
        BaseShield self,
        Grenade source) => ShieldGrenadeCounter.Run(self, () => orig(self, source));

    /// <summary>
    /// 客户端对 mob 的命中只触发表现，跳过 attackTarget.applyHit 与 onHit 回调，
    /// 避免本地扣血/死亡/affect；服务端快照是 mob 状态唯一权威。
    /// </summary>
    public void ApplyHitResult(
        Hook__AttackUtils.orig_applyHitResult orig,
        AttackData atk,
        Entity target)
    {
        if (target is Mob mob && this.isRemoteMob(mob))
        {
            if (atk != null && atk.isSuccess())
            {
                this.playHitVisual(atk, mob);
            }

            return;
        }

        orig(atk, target);
    }

    public static void PostHitResultUpdate(
        Hook__AttackUtils.orig_postHitResultUpdate orig,
        AttackData atk,
        Entity target) => orig(atk, target);

    /// <summary>
    /// 拦截本地伤害。
    /// </summary>
    public AttackData CreateFromHeroAndHit(
        Hook__AttackUtils.orig_createFromHeroAndHit orig,
        Entity parent,
        object baseDmg,
        int? data,
        Entity target)
    {
        var remoteTarget = target is RemoteHero
            || (target is Mob mob && this.isRemoteMob(mob));

        if (parent == null && remoteTarget)
        {
            return null!;
        }

        return orig(parent, baseDmg, data, target);
    }

    /// <summary>
    /// 只允许服务端快照应用 LevelAreaAffect。
    /// </summary>
    public void AreaAffectInit(
        Hook_LevelAreaAffect.orig_init orig,
        LevelAreaAffect self,
        int x,
        int y,
        double durationS,
        int a,
        double aDurationS,
        double aValue,
        ArrayObj affixes)
    {
        if (!this.isApplyingServerAreaAffect())
        {
            return;
        }

        orig(self, x, y, durationS, a, aDurationS, aValue, affixes);
    }

    /// <summary>
    /// 只允许服务端快照应用。
    /// </summary>
    public void AreaAffectApplyAnother(
        Hook_LevelAreaAffect.orig_applyAnotherAffect orig,
        LevelAreaAffect self,
        double a,
        int aDurationS,
        double aValue,
        double affixes,
        ArrayObj prev)
    {
        if (!this.isApplyingServerAreaAffect())
        {
            return;
        }

        orig(self, a, aDurationS, aValue, affixes, prev);
    }

    public static void PowerUpdate(Hook_Power.orig_update orig, Power self)
    {
        if (self == null)
        {
            return;
        }

        var owner = self.owner;
        if (owner == null || owner._level == null)
        {
            try
            {
                self.destroy();
            }
            catch
            {
                // 无法安全销毁时跳过 update
            }

            return;
        }

        orig(self);
    }
}
