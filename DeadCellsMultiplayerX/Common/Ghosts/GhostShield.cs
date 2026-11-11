using System;
using dc.pr;
using dc.tool;
using dc.tool.atk;
using dc.tool.weap;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using Serilog;

namespace DeadCellsMultiplayerX.Common.Ghosts
{
    internal sealed class GhostShield
    {
        private static readonly ILogger Logger =
            Log.ForContext("SourceContext", "GhostShield");

        private readonly GhostWeapons weapons;

        public GhostShield(GhostWeapons weapons)
        {
            this.weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));
        }

        public BaseShield? Get(int slot) => weapons.Get(slot) as BaseShield;

        public bool Prepare(int slot, ItemRef? item)
        {
            if (!weapons.Prepare(slot, item))
                return false;

            return Get(slot) != null;
        }

        /// <summary>
        /// 重放一次盾牌动作
        /// </summary>
        /// <param name="elapsedMs">事件已经过去的时间，用于缩短晚到的弹反窗口</param>
        public bool Apply(
            ShieldActionKind kind,
            int slot,
            double ratio,
            bool fullParry,
            AttackData? sourceAtk,
            double elapsedMs = 0)
        {
            var shield = Get(slot);
            if (shield == null || shield.destroyed)
                return false;

            switch (kind)
            {
                case ShieldActionKind.HoldStart:
                    return StartHold(shield);

                case ShieldActionKind.HoldEnd:
                    return EndHold(shield);

                case ShieldActionKind.ParryStart:
                    return StartParry(shield, elapsedMs);

                case ShieldActionKind.ParryEnd:
                    shield.onShieldEndParry();
                    return true;

                case ShieldActionKind.Block:
                    return true;

                case ShieldActionKind.Counter:
                    if (sourceAtk == null)
                    {
                        // 远端表现：没有真实 AttackData，只播放弹反成功反馈
                        shield.triggerParryFeedbacks();
                        return true;
                    }

                    shield.shieldCounterAttack(sourceAtk, fullParry);
                    return true;

                default:
                    Logger.Warning(
                        "GhostShield unknown action: kind={Kind} slot={Slot}",
                        kind, slot);
                    return false;
            }
        }

        private static bool StartHold(BaseShield shield)
        {
            WeaponSkill? skill = shield.get_curSkill();
            if (skill != null && skill.prepare(null))
                return true;

            shield.onShieldChargeStart();
            return true;
        }

        private static bool EndHold(BaseShield shield)
        {
            WeaponSkill? skill = shield.get_curSkill();
            if (skill != null && skill.chargeF > 0)
            {
                skill.interrupt();
                return true;
            }

            shield.onShieldReleased();
            return true;
        }

        private static bool StartParry(BaseShield shield, double elapsedMs)
        {
            double totalFrames = shield.totalParryDurationS * 60.0;
            double elapsedFrames = Math.Max(0, elapsedMs) * 0.06;

            if (totalFrames > 0 && elapsedFrames >= totalFrames)
                return false;

            if (elapsedFrames > 0 && Game.Class.ME != null)
            {
                shield.startTimeF = Game.Class.ME.ftime - elapsedFrames;
            }

            shield.startParry();
            return true;
        }
    }
}
