namespace DeadCellsMultiplayerX.Client.Integration.Hooks;

using dc;
using dc.en;
using dc.en.inter;
using dc.pr;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs;

/// <summary>
/// 会话 Mob Hook：AI 锁定、固定帧更新、死亡门控、技能初始化与关卡 Mob 挂载。
/// 世界与时钟通过委托按需查询，适配器不持有会话。
/// </summary>
internal sealed class GuestMobHookAdapter(
    Func<ClientMobModifierRegistry?> mobModifiers,
    Func<Mob, bool> isRemoteMob,
    Func<Mob, bool> isDeathSuppressed,
    Func<long> syncedTimeMs,
    CombatDeathGate deathGate)
{
    private readonly Func<ClientMobModifierRegistry?> mobModifiers = mobModifiers
            ?? throw new ArgumentNullException(nameof(mobModifiers));
    private readonly Func<Mob, bool> isRemoteMob = isRemoteMob
            ?? throw new ArgumentNullException(nameof(isRemoteMob));
    private readonly Func<Mob, bool> isDeathSuppressed = isDeathSuppressed
            ?? throw new ArgumentNullException(nameof(isDeathSuppressed));
    private readonly Func<long> syncedTimeMs = syncedTimeMs
            ?? throw new ArgumentNullException(nameof(syncedTimeMs));
    private readonly CombatDeathGate deathGate = deathGate
            ?? throw new ArgumentNullException(nameof(deathGate));

    public void InitSkills(Hook_Mob.orig_initSkills orig, Mob self)
    {
        orig(self);

        this.mobModifiers()?.DispatchInitSkills(
            self, self.oldSkills, self.skills, this.syncedTimeMs());
    }

    public static void AttachMobs(Hook_Level.orig_attachMobs orig, Level self)
    {
        var map = self.map;
        var rooms = map.rooms;
        var colls = map.collisions;
        var spots = map.fastSpots;
        var wid = map.wid;
        var hei = map.hei;
        var collLen = colls.length;
        var spotLen = spots.length;

        var nbTotal = self.nbTotalMobs;

        for (var ri = 0; ri < rooms.length; ri++)
        {
            var room = (dc.level.Room)rooms.getDyn(ri);
            var mobs = room.mobs;
            var mobCount = mobs.length;

            for (var mi = 0; mi < mobCount; mi++)
            {
                var mob = (dc.level.Mob)mobs.getDyn(mi);
                var cx = mob.cx;
                var cy = mob.cy;

                if ((uint)cx >= (uint)wid || (uint)cy >= (uint)hei)
                {
                    continue;
                }

                var idx = cx + (cy * wid);
                int coll = idx < collLen
                         ? colls.getDyn(idx)
                         : 0;
                if ((coll & 1) != 0)
                {
                    continue;
                }

                var bp = Data.Class.mob.byId.get(mob.kind);

                if ((bp.genTags & (1 << 4)) == 0)
                {
                    if (idx >= spotLen)
                    {
                        continue;
                    }

                    var sf = (dc.level.SpotFlags)spots.getDyn(idx);
                    if (sf == null || (sf.high & (1 << 11)) == 0)
                    {
                        continue;
                    }
                }

                if (mob.inShrine)
                {
                    new EliteShrine(self, cx, cy, mob).init();
                }
                else if (mob.inStone)
                {
                    if (self.atManager != null)
                    {
                        new ATStatue(self, cx, cy, mob).init();
                    }
                    else
                    {
                        new MobStatue(self, cx, cy, mob).init();
                    }
                }
                else if (mob.inEgg)
                {
                    new WallEgg(self, cx, cy, mob).init();
                }

                if (mob.kind.ToString() != "BlackBoilerRoom")
                {
                    nbTotal++;
                }
            }
        }

        self.nbTotalMobs = nbTotal;
    }

    public static void OldSkillInterrupt(Hook_OldSkill.orig_interrupt orig, OldSkill self)
    {
        if (self is not OldMobSkill)
        {
            orig(self);
        }
    }

    public static bool OldSkillIsReady(Hook_OldSkill.orig_isReady orig, OldSkill self) => true;

    public static void OldMobSkillUpdate(Hook_OldMobSkill.orig_update orig, OldMobSkill self) => self.MobSkillUpdate();

    public static bool MoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self) => false;

    public void FixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
    {
        orig(self);

        this.mobModifiers()?.OnMobFixedUpdate(self, this.syncedTimeMs());
    }

    public static bool CanUpdate(Hook_Mob.orig_canUpdate orig, Mob self) => true;

    public bool AiLocked(Hook_Mob.orig_aiLocked orig, Mob self) => this.mobModifiers()?.DispatchAiLocked(
            self, this.syncedTimeMs()) ?? true;

    public void OnDie(Hook_Mob.orig_onDie orig, Mob self)
    {
        // 本地预测由服务端确认死亡时允许远端 mob 执行死亡
        if (this.deathGate.IsAllowed(self))
        {
            orig(self);
            return;
        }

        // 其余远端怪物与正在清理的本地怪物不执行本地死亡掉落
        if (this.isRemoteMob(self) || this.isDeathSuppressed(self))
        {
            return;
        }

        orig(self);
    }
}
