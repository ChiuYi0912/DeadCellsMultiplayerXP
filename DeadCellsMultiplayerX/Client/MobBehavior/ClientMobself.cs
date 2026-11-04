using System;
using dc;
using dc.en;
using dc.hl.types;
using dc.level;
using dc.tool;
using dc.tool._Cooldown;
using Hashlink.Marshaling;
using Hashlink.Reflection.Members.Object;
using Hashlink.Reflection.Types;
using HashlinkNET.Native.Impl;
using HaxeProxy.Runtime;
using Serilog;
using Entity = dc.Entity;
using Game = dc.pr.Game;
using Hero = dc.en.Hero;
using Hook_Mob = dc.en.Hook_Mob;
using Level = dc.pr.Level;
using Mob = dc.en.Mob;

namespace DeadCellsMultiplayerX.Client.MobBehavior
{
    public class ClientMobself
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "MobBase");

        public static void ClientfixedUpdate(Mob self)
        {
            HashlinkObjectProto proto =
             ((HashlinkObjectType)HashlinkMarshal.Module.GetTypeByName("Entity")).FindProto("fixedUpdate")!;
            proto.Function.DynamicInvoke(self);

            UpdateThreatAndNemesis(self);
            UpdateAi(self);
            UpdatePassiveState(self);
        }

        #region Mob.fixedUpdate

        private static void UpdateThreatAndNemesis(Mob self)
        {
            if (self.life <= 0 || self.destroyed)
                return;

            if (self.nemesisTarget != null
                && self.nemesisTarget.life <= 0)
                self.nemesisTarget = null;

            self.updateThreat();

            if (self.aTarget != null
                && !self.aTarget.destroyed
                && !self.aTarget.canBeDetected()
                && !ReferenceEquals(self.aTarget, self.nemesisTarget))
                self.setAttackTarget(null);

            var game = self._level?.game;
            var hero = game?.hero;

            bool aggressive =
                game != null
                && game.user.br_hasAggressiveMobs()
                && self.nemesisTarget == null
                && self.aTarget != null
                && ReferenceEquals(self._team, self._level?.teamMob)
                && hero != null
                && hero.life > 0
                && !hero.destroyed;

            bool nearHero =
                aggressive
                && hero!.canBeDetected()
                && Distance(self, hero) <= 9.0;

            if (nearHero)
                self.setNemesisTarget(hero);

            bool validNemesis =
                !ReferenceEquals(self.aTarget, self.nemesisTarget)
                && self.nemesisTarget != null
                && self.nemesisTarget.life > 0
                && !self.nemesisTarget.destroyed;

            if (validNemesis)
            {
                bool keepNemesis = self.nemesisTarget!.canBeDetected()
                    || (GetGenTags(self) & (1 << 2)) != 0;

                if (keepNemesis)
                    self.setNemesisTarget(self.nemesisTarget);
            }

            if (self.elite)
                self.setAffectS(49, 1.0, Ref<double>.In(0.0), null);
        }

        private static void UpdateAi(Mob self)
        {
            if (self.destroyed || self.isUnconscious())
                return;

            if (self.delayedVolte != null)
            {
                if (!self.aiLocked())
                    UpdateDelayedVolte(self);
                return;
            }

            if (!self.aiLocked())
            {
                self.behaviourAi();

                if (self.canUseAggressiveTeleport())
                    self.aggressiveTeleportAi();

                if (self.elite && (GetGenTags(self) & (1 << 7)) == 0)
                {
                    if (HasAffect(self, 34)
                        || self._level!.game.user.br_hasAggressiveMobs())
                        self.eliteTeleportAi();
                }

                if (HasAffect(self, 79))
                    self.necromancedTeleportAi();
            }

            ConsumeQueuedSkill(self);

            if (self.delayedVolte == null && !self.moveBlocked())
                self.move.update();
        }

        private static void UpdateDelayedVolte(Mob self)
        {
            var volte = self.delayedVolte!;
            volte.tf--;

            if (volte.tf > 0)
                return;

            int dir = volte.dir >= 0 ? 1 : -1;
            if (self.onDirChange != null && self.dir != dir)
                self.onDirChange.Invoke(dir);

            self.dir = dir;

            if (self.delayedVolte == null)
            {
                self.onDelayedVolteDone();
            }
            else
            {
                self.onDelayedVolteCancel();
                self.delayedVolte = null;
                self.onDelayedVolteDone();
            }
        }

        private static void ConsumeQueuedSkill(Mob self)
        {
            if (!self.cd.fastCheck.exists(327155712)
                && self.queuedOldSkill != null)
                self.resetQueuedOldSkill();

            if (self.queuedOldSkill == null
                || self.cd.fastCheck.exists(46137344))
                return;

            if (self.life <= 0
                || self.destroyed
                || self.aiLocked()
                || HasAffect(self, 23))
                return;

            var queued = self.queuedOldSkill;
            if (!queued.a.canUse.Invoke())
                return;

            if (!queued.reqTargetInArea)
            {
                self.resetQueuedOldSkill();
                queued.a.prepareOnOwnerTarget(true, queued.data);
            }
            else if (queued.a.getTouchingAreaFor(self.aTarget) != null)
            {
                self.resetQueuedOldSkill();
                queued.a.prepareOnOwnerTarget(true, queued.data);
            }
        }

        private static void UpdatePassiveState(Mob self)
        {
            // 地面/斜坡状态触发的反冲
            if (HasAffect(self, 15)
                && IsOnStableGround(self)
                && !self.cd.fastCheck.exists(331350016))
            {
                double seconds = 0.3 + Random.Shared.NextDouble() * 0.5;
                SetCooldown(self, 331350016, seconds);
                self.recoil(self.dir * (1.0 + Random.Shared.NextDouble()));
            }

            if (self.breachDamage > 0.0 && !self.hasSkillCharging())
            {
                self.breachDamage -= self.breachDamageReduction / 30.0;
                if (self.breachDamage < 0.0)
                    self.breachDamage = 0.0;
            }

            if (self.cd.fastCheck.exists(46137344))
            {
                if (HasAffect(self, 88))
                {
                    self.setAffectS(30, 99999.0, Ref<double>.In(0.0), null);
                    self.kill();
                }
                else if (!self.cd.fastCheck.exists(339738624)
                    && self.revive.isReady())
                {
                    self.revive.prepare(null);
                }
            }

            // 飞行单位离开实心 tile 时维护 lastStablePt
            if (self.get_flying()
                && !IsSolidAt(self._level!.map, self.cx, self.cy))
            {
                self.lastStablePt.cx = self.cx;
                self.lastStablePt.cy = self.cy;
            }

            UpdateCinematic(self);
            UpdateHidden(self);

            self.minimapTracking();

            if (ReferenceEquals(self._team, self._level!.teamMob))
                ApplyAreaAffects(self);

            UpdateElite(self);
        }

        private static void UpdateCinematic(Mob self)
        {
            if (!self._level!.game.hasCinematic())
                return;

            self.setAffectS(9, 1.0, Ref<double>.In(0.0), null);

            if (self.elite && !self.cd.fastCheck.exists(341835776))
            {
                SetCooldown(self, 341835776, 0.2);
                self.interruptRunningEliteSkills();
            }
        }

        private static void UpdateHidden(Mob self)
        {
            if (!self.hidden)
                return;

            if (self.elite)
            {
                self.reveal();
                return;
            }

            if (HasAffect(self, 8)
                || self.cd.fastCheck.exists(343932928))
                return;

            var heroes = self._level!.entitiesByClass.get(60929) as ArrayObj;
            if (heroes == null)
                return;

            for (int i = 0; i < heroes.length; i++)
            {
                if (heroes.getDyn(i) is not Hero hero)
                    continue;

                if (!self.canBeRevealedBy(hero))
                    continue;

                self.revealer = hero;
                self.onDetectRevealer(hero);
            }
        }

        private static void ApplyAreaAffects(Mob self)
        {
            var level = self._level!;

            var atFeet = GetAreaAffect(level, self.cx, self.cy);
            atFeet?.applyTo(self);

            var atHead = GetAreaAffect(level, self.cx, self.cy - 1);
            atHead?.applyTo(self);
        }

        private static void UpdateElite(Mob self)
        {
            if (!self.elite)
                return;

            if (!HasAffect(self, 35)
                && !HasAffect(self, 34)
                && self.getLifeRatio() < 0.55)
                self.eliteAnger(Ref<bool>.In(false));

            if (self.eSpecialAtkUpdate == null
                || self.life <= 0
                || self.destroyed
                || self.cd.fastCheck.exists(62914560))
                return;

            self.eSpecialAtkUpdate.Invoke();
        }

        #endregion

        #region behaviourAi

        public void ClientbehaviourAi(Mob self)
        {
            if (self.nemesisTarget != null)
            {
                self.setAttackTarget(self.nemesisTarget);
                self.move.@goto(self.nemesisTarget.cx, self.cy);
                return;
            }

            if (self.aTarget == null || !self.aTarget.canBeDetected())
            {
                self.behaviour_platformPatrol();
                return;
            }

            if (!IsOnStableGround(self) || !IsOnStableGround(self.aTarget))
                return;

            if (!self.move.hasTarget() || self.canUpdateMove())
            {
                if (!self.move.@goto(self.aTarget.cx, self.aTarget.cy))
                    self.behaviour_platformPatrol();
            }
        }

        #endregion

        #region source helpers

        private static int GetGenTags(Mob self)
        {
            if (self._infos == null)
            {
                self._infos = ((HaxeProxyBase)Data.Class.mob.byId.get(self.type)).ToVirtual<Hashlink.Virtuals.virtual_active_blueprints_canBeElite_colorSwaps_dlc_flesh1_flesh2_genTags_glowInnerColor_glowOuterColor_group_icon_id_index_life_maxPerPlatform_maxPerRoom_metaItems_minPfHeight_minPfSize_mobTags_name_newSkill_particles_pfCost_props_score_skill_volteDelay_weight_>();
            }

            return self._infos.genTags;
        }

        private static bool HasAffect(Mob self, int index)
        {
            var affects = self.affects;
            if (affects == null || index >= affects.length)
                return false;

            return affects.getDyn(index) is ArrayObj list
                && list.length > 0;
        }

        private static void SetCooldown(Mob self, int key, double seconds)
        {
            var cd = self.cd;
            if (cd == null)
                return;

            double frames = System.Math.Floor(seconds * cd.baseFps * 1000.0) / 1000.0;
            if (cd.fastCheck.get(key) is CdInst existing)
            {
                existing.frames = frames;
                return;
            }

            var inst = new CdInst(key, frames);
            cd.fastCheck.set(key, inst);
            cd.cdList.push(inst);
        }

        private static LevelAreaAffect? GetAreaAffect(
            Level level, int x, int y)
        {
            var map = level.map;
            if (map == null
                || x < 0 || x >= map.wid
                || y < 0 || y >= map.hei)
                return null;

            var areas = level.fastAoeAccess;
            int index = x + y * map.wid;
            if ((uint)index >= (uint)areas.length)
                return null;

            return areas.getDyn(index) as LevelAreaAffect;
        }

        private static double Distance(Entity a, Entity b)
        {
            const double lineOffset = 1.0 / 48.0;

            double ax = a.cx + a.xr;
            double ay = a.cy + a.yr - lineOffset * a.hei;
            double bx = b.cx + b.xr;
            double by = b.cy + b.yr - lineOffset * b.hei;

            double dx = ax - bx;
            double dy = ay - by;
            return System.Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 判断实体是否站在稳定地面/斜坡环境
        /// </summary>
        private static bool IsOnStableGround(Entity entity)
        {
            var map = entity._level?.map;
            if (map == null)
                return false;

            int cx = entity.cx;
            int cy = entity.cy;

            if (IsSolidAt(map, cx, cy + 1))
            {
                double capturedYr = entity.yr;
                double xr = entity.xr;
                double yr = entity.yr;
                double groundYr = map.getGroundYr(
                    cx, cy, Ref<double>.In(xr), Ref<double>.In(yr));

                if (capturedYr > groundYr && entity.dy == 0.0)
                    return true;
            }

            if (entity.ignoreSlopes)
                return false;

            if (Lib_std.math_abs.Invoke(entity.dy) >= 0.1)
                return false;

            return IsCollisionBitSet(map, cx, cy, 0x200);
        }

        private static bool IsSolidAt(LevelMap map, int x, int y)
        {
            int wid = map.wid;
            int hei = map.hei;

            if (x < 0 || x >= wid || y < 0 || y >= hei)
                return true;

            var collisions = map.collisions;
            int idx = x + y * wid;

            if ((uint)idx >= (uint)collisions.length)
                idx = 0;

            int value = collisions.getDyn(idx);
            return (value & 1) != 0;
        }

        private static bool IsCollisionBitSet(
            LevelMap map, int x, int y, int bitMask)
        {
            int wid = map.wid;
            int hei = map.hei;

            if (x < 0 || x >= wid || y < 0 || y >= hei)
                return false;

            var collisions = map.collisions;
            int idx = x + y * wid;

            if ((uint)idx >= (uint)collisions.length)
                idx = 0;

            int value = collisions.getDyn(idx);
            return (value & bitMask) != 0;
        }

        #endregion
    }
}
