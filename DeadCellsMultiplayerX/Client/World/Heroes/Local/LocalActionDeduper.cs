using dc.tool.weap;

namespace DeadCellsMultiplayerX.Client.World.Heroes.Local
{
    /// <summary>
    /// 本地动作的单调 ActionId 与盾牌姿态去重。
    /// </summary>
    internal sealed class LocalActionDeduper
    {
        private sealed class ShieldState
        {
            public bool Holding;
            public bool Parrying;
        }

        private long nextActionId;

        /// <summary>
        /// 每个本地盾牌当前的姿态，用于抑制重复的起止事件。
        /// </summary>
        private readonly Dictionary<BaseShield, ShieldState> shieldStates =
            new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// 取得下一个单调 ActionId。
        /// </summary>
        public long NextActionId() => ++nextActionId;

        /// <summary>
        /// 返回 false 表示已经在蓄力中，不应重复上报 HoldStart。
        /// </summary>
        public bool TryBeginHold(BaseShield shield)
        {
            var state = GetShieldState(shield);
            if (state.Holding)
                return false;

            state.Holding = true;
            return true;
        }

        public void EndHold(BaseShield shield)
            => GetShieldState(shield).Holding = false;

        /// <summary>
        /// 返回 false 表示已经在弹反中，不应重复上报 ParryStart。
        /// </summary>
        public bool TryBeginParry(BaseShield shield)
        {
            var state = GetShieldState(shield);
            if (state.Parrying)
                return false;

            state.Parrying = true;
            return true;
        }

        /// <summary>
        /// 返回 false 表示当前不在弹反中，不应上报 ParryEnd。
        /// </summary>
        public bool TryEndParry(BaseShield shield)
        {
            var state = GetShieldState(shield);
            if (!state.Parrying)
                return false;

            state.Parrying = false;
            return true;
        }

        public void Clear() => shieldStates.Clear();

        private ShieldState GetShieldState(BaseShield shield)
        {
            if (!shieldStates.TryGetValue(shield, out var state))
                shieldStates[shield] = state = new ShieldState();

            return state;
        }
    }
}
