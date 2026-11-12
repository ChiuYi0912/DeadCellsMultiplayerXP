using dc;
using dc.en;
using dc.en.inter;
using dc.pr;
using dc.tool.skill;
using DeadCellsMultiplayerX.Client.Presentation.Combat;
using DeadCellsMultiplayerX.Client.World.Mobs.Behavior;
using DeadCellsMultiplayerX.Engine.Adapters.Mobs;

namespace DeadCellsMultiplayerX.Client.Integration.Hooks
{
    /// <summary>
    /// 会话 Mob Hook：AI 锁定、固定帧更新、死亡门控、技能初始化与关卡 Mob 挂载。
    /// 世界与时钟通过委托按需查询，适配器不持有会话。
    /// </summary>
    internal sealed class GuestMobHookAdapter
    {
        private readonly Func<ClientMobModifierRegistry?> mobModifiers;
        private readonly Func<Mob, bool> isRemoteMob;
        private readonly Func<Mob, bool> isDeathSuppressed;
        private readonly Func<long> syncedTimeMs;
        private readonly CombatDeathGate deathGate;

        public GuestMobHookAdapter(
            Func<ClientMobModifierRegistry?> mobModifiers,
            Func<Mob, bool> isRemoteMob,
            Func<Mob, bool> isDeathSuppressed,
            Func<long> syncedTimeMs,
            CombatDeathGate deathGate)
        {
            this.mobModifiers = mobModifiers
                ?? throw new ArgumentNullException(nameof(mobModifiers));
            this.isRemoteMob = isRemoteMob
                ?? throw new ArgumentNullException(nameof(isRemoteMob));
            this.isDeathSuppressed = isDeathSuppressed
                ?? throw new ArgumentNullException(nameof(isDeathSuppressed));
            this.syncedTimeMs = syncedTimeMs
                ?? throw new ArgumentNullException(nameof(syncedTimeMs));
            this.deathGate = deathGate
                ?? throw new ArgumentNullException(nameof(deathGate));
        }

        public void InitSkills(Hook_Mob.orig_initSkills orig, Mob self)
        {
            orig(self);

            mobModifiers()?.DispatchInitSkills(
                self, self.oldSkills, self.skills, syncedTimeMs());
        }

        public void AttachMobs(Hook_Level.orig_attachMobs orig, Level self)
        {
            var map = self.map;
            var rooms = map.rooms;
            var colls = map.collisions;
            var spots = map.fastSpots;
            int wid = map.wid;
            int hei = map.hei;
            int collLen = colls.length;
            int spotLen = spots.length;

            int nbTotal = self.nbTotalMobs;

            for (int ri = 0; ri < rooms.length; ri++)
            {
                var room = (dc.level.Room)rooms.getDyn(ri);
                var mobs = room.mobs;
                int mobCount = mobs.length;

                for (int mi = 0; mi < mobCount; mi++)
                {
                    var mob = (dc.level.Mob)mobs.getDyn(mi);
                    int cx = mob.cx;
                    int cy = mob.cy;

                    if ((uint)cx >= (uint)wid || (uint)cy >= (uint)hei)
                    {
                        continue;
                    }

                    int idx = cx + cy * wid;
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

        public void OldSkillInterrupt(Hook_OldSkill.orig_interrupt orig, OldSkill self)
        {
            if (self is not OldMobSkill)
            {
                orig(self);
            }
        }

        public bool OldSkillIsReady(Hook_OldSkill.orig_isReady orig, OldSkill self)
        {
            return true;
        }

        public void OldMobSkillUpdate(Hook_OldMobSkill.orig_update orig, OldMobSkill self)
        {
            self.MobSkillUpdate();
        }

        public bool MoveBlocked(Hook_Mob.orig_moveBlocked orig, Mob self)
        {
            return false;
        }

        public void FixedUpdate(Hook_Mob.orig_fixedUpdate orig, Mob self)
        {
            orig(self);

            mobModifiers()?.OnMobFixedUpdate(self, syncedTimeMs());
        }

        public bool CanUpdate(Hook_Mob.orig_canUpdate orig, Mob self)
        {
            return true;
        }

        public bool AiLocked(Hook_Mob.orig_aiLocked orig, Mob self)
        {
            return mobModifiers()?.DispatchAiLocked(
                self, syncedTimeMs()) ?? true;
        }

        public void OnDie(Hook_Mob.orig_onDie orig, Mob self)
        {
            // 本地预测由服务端确认死亡时允许远端 mob 执行死亡
            if (deathGate.IsAllowed(self))
            {
                orig(self);
                return;
            }

            // 其余远端怪物与正在清理的本地怪物不执行本地死亡掉落
            if (isRemoteMob(self) || isDeathSuppressed(self))
            {
                return;
            }

            orig(self);
        }
    }
}
