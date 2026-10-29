using System.Collections.Generic;

namespace DeadCellsMultiplayerX.Server.MobAI.Brains
{
    /// <summary>
    /// Mob Brain 的一次决策上下文。
    /// 只包含纯数据，不引用游戏实体。
    /// </summary>
    public sealed class MobBrainContext
    {
        /// <summary>
        /// 当前 mob 的 NetId。
        /// </summary>
        public uint MobNetId { get; init; }

        /// <summary>
        /// 当前服务端 Tick。
        /// </summary>
        public long Tick { get; init; }

        /// <summary>
        /// 自身运行时状态。
        /// </summary>
        public MobRuntimeState State { get; init; } = new();

        /// <summary>
        /// 当前可见/可攻击的目标。
        /// </summary>
        public IReadOnlyList<MobTargetCandidate> Targets { get; init; } = [];

        /// <summary>
        /// 本次决策的随机种子。
        /// </summary>
        public long Seed { get; init; }

        /// <summary>
        /// 所在关卡。
        /// </summary>
        public string LevelId { get; init; } = string.Empty;

        /// <summary>
        /// 所在子关卡。
        /// </summary>
        public int SubLevelIndex { get; init; } = -1;
    }

    /// <summary>
    /// Mob 运行时状态。
    /// </summary>
    public sealed class MobRuntimeState
    {
        /// <summary>
        /// 当前生命。
        /// </summary>
        public int Life { get; init; }

        /// <summary>
        /// 最大生命。
        /// </summary>
        public int MaxLife { get; init; }

        /// <summary>
        /// 当前硬直/失衡值。
        /// </summary>
        public int Poise { get; init; }

        /// <summary>
        /// 当前正在执行的 ActionId；0 表示空闲。
        /// </summary>
        public long CurrentActionId { get; init; }

        /// <summary>
        /// 技能冷却状态。
        /// </summary>
        public IReadOnlyList<MobSkillCooldown> Skills { get; init; } = [];
    }

    /// <summary>
    /// 单个技能的冷却状态。
    /// </summary>
    public sealed class MobSkillCooldown
    {
        /// <summary>
        /// 技能 id。
        /// </summary>
        public string SkillId { get; init; } = string.Empty;

        /// <summary>
        /// 可再次使用的 Tick。
        /// </summary>
        public long ReadyTick { get; init; }
    }

    /// <summary>
    /// 候选目标。
    /// </summary>
    public sealed class MobTargetCandidate
    {
        /// <summary>
        /// 目标玩家 guid。
        /// </summary>
        public string Guid { get; init; } = string.Empty;

        /// <summary>
        /// 目标 NetId。
        /// </summary>
        public uint NetId { get; init; }

        /// <summary>
        /// 目标位置 X。
        /// </summary>
        public double X { get; init; }

        /// <summary>
        /// 目标位置 Y。
        /// </summary>
        public double Y { get; init; }

        /// <summary>
        /// 目标当前生命。
        /// </summary>
        public int Life { get; init; }

        /// <summary>
        /// 仇恨值。
        /// </summary>
        public double Threat { get; init; }

        /// <summary>
        /// 与 mob 的距离平方。
        /// </summary>
        public double DistanceSq { get; init; }

        /// <summary>
        /// 是否在视野内。
        /// </summary>
        public bool Visible { get; init; }
    }
}
