namespace DeadCellsMultiplayerX.Engine.Adapters.Heroes;

using dc.pr;
using dc.tool;
using dc.tool.weap;
using DeadCellsMultiplayerX.Contracts.Actions;
using Serilog;

internal sealed class GhostShield(GhostWeapons weapons)
{
    private static readonly ILogger Logger =
        Log.ForContext("SourceContext", "GhostShield");

    private readonly GhostWeapons weapons = weapons ?? throw new ArgumentNullException(nameof(weapons));

    public BaseShield? Get(int slot) => this.weapons.Get(slot) as BaseShield;

    public bool Prepare(int slot, ItemRef? item)
    {
        if (!this.weapons.Prepare(slot, item))
        {
            return false;
        }

        return this.Get(slot) != null;
    }

    /// <summary>
    /// 把客户端相位重放到服务端隐形盾；格挡/反击由引擎原生判定。
    /// </summary>
    /// <param name="elapsedMs">事件已经过去的时间，用于缩短晚到的弹反窗口</param>
    public bool Apply(
        ShieldIntentKind kind,
        int slot,
        double elapsedMs = 0)
    {
        var shield = this.Get(slot);
        if (shield == null || shield.destroyed)
        {
            return false;
        }

        switch (kind)
        {
            case ShieldIntentKind.HoldStart:
                return StartHold(shield);

            case ShieldIntentKind.HoldEnd:
                return EndHold(shield);

            case ShieldIntentKind.ParryStart:
                return StartParry(shield, elapsedMs);

            case ShieldIntentKind.ParryEnd:
                shield.onShieldEndParry();
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
        {
            return true;
        }

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
        var totalFrames = shield.totalParryDurationS * 60.0;
        var elapsedFrames = Math.Max(0, elapsedMs) * 0.06;

        if (totalFrames > 0 && elapsedFrames >= totalFrames)
        {
            return false;
        }

        if (elapsedFrames > 0 && Game.Class.ME != null)
        {
            shield.startTimeF = Game.Class.ME.ftime - elapsedFrames;
        }

        shield.startParry();
        return true;
    }
}
