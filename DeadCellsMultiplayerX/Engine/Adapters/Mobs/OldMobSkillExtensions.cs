using dc.en;
using dc.hl.types;
using dc.tool._Cooldown;
using dc.tool.skill;
using Hashlink.Marshaling;
using Hashlink.Reflection.Members.Object;
using Hashlink.Reflection.Types;
namespace DeadCellsMultiplayerX.Engine.Adapters.Mobs
{
    public static class OldMobSkillExtensions
    {
        // OldSkill.update
        private static HashlinkObjectProto? oldSkillUpdateProto;

        public static void UpdateReplacementBase(this OldMobSkill self)
        {
            if (self == null)
                return;

            if (self.owner is not Mob owner || owner.life <= 0)
                return;

            var cd = owner.cd;

            // 1. 蓄力中断检查（保持原版三个独立 if）
            if (self.chargeF > 0
                && self.interruptOnNoTarget
                && owner.aTarget == null)
            {
                self.interrupt();
            }

            if (self.chargeF > 0
                && !self.allowInAir
                && !cd.fastCheck.exists(39845888))
            {
                self.interrupt();
            }

            if (self.chargeF > 0
                && !owner.canUpdate())
            {
                self.interrupt();
            }

            // 2. 调用 base.update()（OldSkill.update），避免虚调用回环
            var proto = oldSkillUpdateProto;
            if (proto == null)
            {
                var type = (HashlinkObjectType)HashlinkMarshal.Module
                    .GetTypeByName("tool.skill.OldSkill");
                proto = type.FindProto("update")
                    ?? throw new InvalidOperationException("Cannot resolve OldSkill.update proto");
                oldSkillUpdateProto = proto;
            }
            proto.Function.DynamicInvoke(self);

            // 3. 自动准备技能
            if (self.isReady()
                && !self.manual
                && owner.canUpdate()
                && (self.allowInAir || cd.fastCheck.exists(39845888))
                && !cd.fastCheck.exists(715128832)
                && owner.aTarget != null
                && !owner.aTarget.destroyed
                && owner.queuedOldSkill == null
                && self._canUse()
                && (!owner.hasSkillCharging() || self.canCancelOtherSkills)
                && (!owner.aiLocked()
                    || (owner.hasSkillCharging() && self.canCancelOtherSkills))
                && (!cd.fastCheck.exists(329252864) || self.ignoreGlobalCD)
                && !owner.isUnconscious())
            {
                var target = owner.aTarget;
                var affects = owner.affects;

                bool noStun = !HasAffect(affects, 9);
                bool noFreeze = !HasAffect(affects, 23);

                if (noStun
                    && noFreeze
                    && self.getTouchingAreaFor(target) != null
                    && self._canUseOn(target)
                    && !self.hasSanctuaryProtection(target))
                {
                    bool needTurn = false;
                    if (self.needFacing())
                    {
                        double ownerX = (owner.cx + owner.xr) * 24.0;
                        double targetX = (target.cx + target.xr) * 24.0;
                        int desired = targetX < ownerX ? -1 : 1;
                        needTurn = owner.dir != desired;
                    }

                    if (needTurn)
                        owner.lookAtDelayed(target);
                    else
                        self.prepareOnOwnerTarget(null, null);
                }
            }

            // 4. 蓄力公告
            double chargeF = self.chargeF;
            if (chargeF <= 0)
                return;

            if (!self.announced
                && 0.32 * self.getCastSpeed() > self.framesToSec(chargeF))
            {
                self.announce();
            }

            chargeF = self.chargeF;
            if (chargeF <= 0)
                return;

            // 5. 临近释放时刷新 580911104 冷却
            double remainingSec = self.framesToSec(chargeF);
            if (0.32 + 0.2 > remainingSec)
            {
                double frames =
                    Math.Floor((remainingSec + 0.2) * cd.baseFps * 1000.0) / 1000.0;

                if (cd.fastCheck.get(580911104) is CdInst existing)
                {
                    existing.frames = frames;
                }
                else
                {
                    var inst = new CdInst(580911104, frames);
                    cd.fastCheck.set(580911104, inst);
                    cd.cdList.push(inst);
                }
            }

            if (self.chargeF < 0.01)
            {
                self.onChargingArea(self.chargeArea, 1.0);
                return;
            }

            double denom = self.chargeMaxF * self.chargeMul;
            double ratio = denom != 0.0
                ? 1.0 - self.chargeF / denom
                : 0.0;

            if (ratio < 0.0)
                ratio = 0.0;
            else if (ratio > 1.0)
                ratio = 1.0;

            self.onChargingArea(self.chargeArea, ratio);
        }


        public static void MobSkillUpdate(this OldMobSkill self)
        {
            if (self == null)
                return;

            if (self.owner is not Mob owner || owner.life <= 0)
                return;

            var cd = owner.cd;

            var proto = oldSkillUpdateProto;
            if (proto == null)
            {
                var type = (HashlinkObjectType)HashlinkMarshal.Module
                    .GetTypeByName("tool.skill.OldSkill");
                proto = type.FindProto("update")
                    ?? throw new InvalidOperationException("Cannot resolve OldSkill.update proto");
                oldSkillUpdateProto = proto;
            }
            proto.Function.DynamicInvoke(self);

            double chargeF = self.chargeF;
            if (chargeF <= 0)
                return;

            if (!self.announced
                && 0.32 * self.getCastSpeed() > self.framesToSec(chargeF))
            {
                self.announce();
            }

            chargeF = self.chargeF;
            if (chargeF <= 0)
                return;

            double remainingSec = self.framesToSec(chargeF);
            if (0.32 + 0.2 > remainingSec)
            {
                double frames =
                    Math.Floor((remainingSec + 0.2) * cd.baseFps * 1000.0) / 1000.0;

                if (cd.fastCheck.get(580911104) is CdInst existing)
                {
                    existing.frames = frames;
                }
                else
                {
                    var inst = new CdInst(580911104, frames);
                    cd.fastCheck.set(580911104, inst);
                    cd.cdList.push(inst);
                }
            }

            if (self.chargeF < 0.01)
            {
                self.onChargingArea(self.chargeArea, 1.0);
                return;
            }

            double denom = self.chargeMaxF * self.chargeMul;
            double ratio = denom != 0.0
                ? 1.0 - self.chargeF / denom
                : 0.0;

            if (ratio < 0.0)
                ratio = 0.0;
            else if (ratio > 1.0)
                ratio = 1.0;

            self.onChargingArea(self.chargeArea, ratio);
        }

        private static bool HasAffect(ArrayObj affects, int index)
        {
            if (affects == null || index < 0 || index >= affects.length)
                return false;

            return affects.getDyn(index) is ArrayObj list
                && list.length > 0;
        }
    }
}
