using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using dc.en.mob;
using dc.libs.heaps.slib._AnimManager;
using dc.pr;
using dc.spine;
using dc.tool;
using dc.tool._Cooldown;
using dc.tool.atk;
using dc.tool.skill;
using Hashlink.Marshaling;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;
using Hashlink.Reflection.Members.Object;
using Hashlink.Reflection.Types;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore.Utilities;
using TestMobs.Utils;

namespace TestMobs.Mobs
{
    public class ZombieTest : MobTestBase<Zombie>
    {
        private readonly ConditionalWeakTable<Zombie, OldMobSkill> skills = new();

        public override void InitHook()
        {
            Hook_Zombie.fixedUpdate += Hook_Zombie_fixedUpdate;
            Hook_Zombie.initSkills += Hook_Zombie_initSkills;
        }

        private void Hook_Zombie_initSkills(Hook_Zombie.orig_initSkills orig, Zombie self)
        {
            //HashlikUtils.InvokeHashlinkMethod(self, "initSkills");
            ((HashlinkObjectType)self.HashlinkObj.Type).Super!.FindProto("initSkills")!.Function.DynamicInvoke(self);
            dc.ui.Console.Class.ME.flags.set("area".AsHaxeString(), 1);
            // HashlinkObjectProto proto =
            // ((HashlinkObjectType)HashlinkMarshal.Module.GetTypeByName("en.Mob")).FindProto("initSkills")!;
            // proto.Function.DynamicInvoke(self);

            InitCcSkill(self);
            InitJumpSkill(self);

            //lock不会影响手动调用技能
            self.lockAiS(999999);
        }

        private void InitCcSkill(Zombie self)
        {
            var infos = self.getOldSkillInfos("cc".AsHaxeString());

            //开启manual状态机自动忽略此技能事件
            //infos.manual = true;

            var skill = self.createOldMobSkill(infos);


            
            //设置攻击范围
            skill.addFrontCircle(infos.radius, false, null);

            skill.dynOnChargeStart = () =>
            {
                self.spr.get_anim().play("atkLoadA".AsHaxeString(), null, null).loop(null);
                skill.playChargeSfx(null, null, Ref<bool>.Null, Ref<double>.Null, null);
            };

            skill.dynOnAttackAnim = () =>
            {
                self.spr.get_anim().play("atkA".AsHaxeString(), null, null);
                skill.playReleaseSfx(null, null, Ref<bool>.Null, Ref<double>.Null, null);
            };

            skill.dynOnChargeComplete = () =>
            {
                var x = (self.cx + self.xr) * 24.0;
                var y = (self.cy + self.yr) * 24.0 - self.hei * 0.5;
                self._level.fx.playEnmAnim(
                    "trackerAtkFx/fxTrackerAtk".AsHaxeString(),
                    x, y, self.dir, 0.75,
                    skill.getCastSpeed(),
                    null, Ref<double>.Null);
            };

            skill.dynOnTouchEntity = e =>
            {
                AttackUtils.Class.createFromMobAndHit(self, infos.power, null, e);
            };
        }

        private void InitJumpSkill(Zombie self)
        {
            var infos = self.getOldSkillInfos("jump".AsHaxeString());
            var skill = self.createOldMobSkill(infos);
            skills.AddOrUpdate(self, skill);
            skill.addFrontRect((double)infos.props.distance!, 3.0, 3, null, null);

            skill.canUseOn = e => self.get_pf() != null && e.get_pf() == self.get_pf();

            skill.dynOnExecute = _ => ExecuteJumpSkill(self, infos, skill);

            // 蓄力开始：蓄力音效 + 循环 atkLoadB 动画
            skill.dynOnChargeStart = () =>
            {
                skill.playChargeSfx(null, null, Ref<bool>.Null, Ref<double>.Null, null);
                self.spr.get_anim().play("atkLoadB".AsHaxeString(), null, null).loop(null);
            };

            // 跳跃命中判定区域，跟随僵尸本身（相对位置 0,0）
            self.jumpHit = new Area(24.0, null);
            self.jumpHit.setRelativePos(self, 0.0, 0.0);
        }

        private void ExecuteJumpSkill(
            Zombie self,
            virtual_allowInAir_allowSameSkillNearby_breachResistance_charge_chargeAnim_chargeAnimSpd_coolDown_disableAnnounceFx_execAnim_execAnimSpd_fxId_hitFrame_id_ignoreGlobalCD_lockAiAfter_manual_power_props_radius_sfxCharge_sfxHit_sfxRelease_ infos,
            OldMobSkill skill)
        {
            // 播放 atkB，并把最后一层动画速度放慢到 0.5（起跳起手）
            var anim = self.spr.get_anim().play("atkB".AsHaxeString(), null, null);
            var stack = anim.stack;
            if (!anim.destroyed && stack.length > 0)
            {
                ((AnimInstance)((HashlinkObject)stack.array[stack.length - 1]!).AsHaxe()).speed = 0.5;
            }

            //起跳特效
            self._level.fx.jumpFrom(
                (self.cx + self.xr) * 24.0,
                (self.cy + self.yr) * 24.0,
                self.dir,
                16711680);

            // 空中冲刺拖尾特效
            var fx = self._level.fx.playAnimOld(
                "fxAirDash".AsHaxeString(),
                (self.cx + self.xr) * 24.0,
                (self.cy + self.yr) * 24.0,
                1, 0.5, 1, false, Ref<bool>.Null);
            fx.a = 0.4;
            var tile = fx.t;
            tile.dx = -(int)(0.5 * tile.width);
            tile.dy = -(int)(0.7 * tile.height);
            fx.animXr = 0.5;
            fx.animYr = 0.7;

            //水平跳跃
            if (!self.moveBlocked())
                self.get_mvWalk().hJump((double)(self.dir * (infos.props.distance + 3.0))!, 1.2, 2.5, false);

            // 标记 "跳跃攻击进行中"
            //    fixedUpdate / onLand / applyJumpHit 都会检查这个键
            const int K_JUMP_ACTIVE = 1226833920;
            var duration = Math.Floor(self.cd.baseFps * 1000.0) / 1000.0;

            var old = self.cd.fastCheck.get(K_JUMP_ACTIVE);
            if (old != null)
            {
                old.frames = duration;
                skill.playReleaseSfx(null, null, Ref<bool>.Null, Ref<double>.Null, null);
                return;
            }

            var inst = new CdInst(K_JUMP_ACTIVE, duration);
            self.cd.fastCheck.set(K_JUMP_ACTIVE, inst);
            self.cd.cdList.push(inst);
            skill.playReleaseSfx(null, null, Ref<bool>.Null, Ref<double>.Null, null);
        }

        private void Hook_Zombie_fixedUpdate(Hook_Zombie.orig_fixedUpdate orig, Zombie self)
        {
            HashlikUtils.InvokeHashlinkMethod(self, "fixedUpdate");

            const int K_JUMP_ACTIVE = 1226833920;
            if (!self.cd.fastCheck.exists(K_JUMP_ACTIVE))
                return;

            var pt = self.lastStablePt;
            pt.cx = self.cx;
            pt.cy = self._level.map.getGroundY(self.cx, self.cy);

            self._level.fx.tailLine(self, 16711680, 0.7, 3.0, null, Ref<double>.Null, Ref<double>.Null);

            if (!self.isUnconscious() && !self.moveBlocked())
            {
                self.applyJumpHit();
                return;
            }

            var inst = self.cd.fastCheck.get(K_JUMP_ACTIVE);
            if (inst != null)
            {
                self.cd.cdList.remove(inst);
                inst.frames = 0.0;
                self.cd.fastCheck.remove(inst.k);
            }

            self.applyJumpHit();
        }

        public override void OnFrameUpdate()
        {
            base.OnFrameUpdate();

            // 1. 检测 N 键是否按下
            if (!dc.hxd.Key.Class.isPressed(78))
                return;

            // 2. 获取当前玩家
            var hero = Game.Class.ME?.hero;
            if (hero == null || hero.destroyed)
                return;

            Zombie closestZombie = null!;
            OldMobSkill closestSkill = null!;
            double minDistance = double.MaxValue;

            foreach (var kvp in skills)
            {
                var zombie = kvp.Key;
                var skill = kvp.Value;

                if (zombie.destroyed || zombie.life <= 0)
                    continue;

                double dx = zombie.cx - hero.cx;
                double dy = zombie.cy - hero.cy;
                double distance = dx * dx + dy * dy;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestZombie = zombie;
                    closestSkill = skill;
                }
            }

            // 确保僵尸有目标
            if (closestZombie.aTarget == null)
            {
                //没有目标技能无法释放
                closestZombie.setAttackTarget(hero);
            }

            if (closestSkill != null && closestZombie != null)
            {
                if (closestSkill.isReady())
                {
                    logger.Information("触发最近的 Zombie 技能 cc2");

                    //此为手动释放技能,不会应用伤害和计算攻击范围
                    //closestSkill.prepare(null);

                    //对一个目标释放完整技能
                    closestSkill.prepareOnOwnerTarget(true, null);
                }
                else
                {
                    logger.Warning("最近的 Zombie 技能未就绪（冷却中或正在施法）");
                }
            }
        }
    }
}