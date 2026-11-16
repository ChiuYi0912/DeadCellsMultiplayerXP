namespace DeadCellsMultiplayerX.Simulation.Actions;

using DeadCellsMultiplayerX.Contracts.Actions;

/// <summary>
/// 服务端盾牌相位守卫。
/// 不重新判定 Block/Partial；只防止重复相位把弹反窗口无限续期。
/// </summary>
internal sealed class HeroShieldStateMachine
{
    /// <summary>
    /// 任何非 Idle 相位超过该时长视为客户端丢失了结束相位，允许重新开始。
    /// </summary>
    public const long MAX_PHASE_MS = 2000;

    private enum Phase
    {
        Idle,
        Holding,
        ParryWindow,
    }

    private Phase state = Phase.Idle;
    private long lastTransitionMs = long.MinValue;

    public bool CanTransition(ShieldIntentKind intent, long nowMs)
    {
        var current = this.EffectiveState(nowMs);

        return intent switch
        {
            ShieldIntentKind.HoldStart => current == Phase.Idle,
            ShieldIntentKind.HoldEnd => current == Phase.Holding,
            ShieldIntentKind.ParryStart => current != Phase.ParryWindow,
            ShieldIntentKind.ParryEnd => current == Phase.ParryWindow,
            _ => false,
        };
    }

    public void Commit(ShieldIntentKind intent, long nowMs)
    {
        this.state = intent switch
        {
            ShieldIntentKind.HoldStart => Phase.Holding,
            ShieldIntentKind.HoldEnd => Phase.Idle,
            ShieldIntentKind.ParryStart => Phase.ParryWindow,
            ShieldIntentKind.ParryEnd => Phase.Idle,
            _ => this.state,
        };

        this.lastTransitionMs = nowMs;
    }

    public void Reset()
    {
        this.state = Phase.Idle;
        this.lastTransitionMs = long.MinValue;
    }

    private Phase EffectiveState(long nowMs)
        => this.state != Phase.Idle
            && this.lastTransitionMs != long.MinValue
            && nowMs - this.lastTransitionMs > MAX_PHASE_MS
                ? Phase.Idle
                : this.state;
}
