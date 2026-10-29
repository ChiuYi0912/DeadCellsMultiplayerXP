using System.Collections.Generic;

namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// 服务端广播的一次 mob 技能攻击计划。
    /// 客户端据此提前播放 windup、预判 hit window，并进行盾反/闪避。
    /// </summary>
    public sealed class MobActionPlan
    {
        /// <summary>
        /// 世界重置代号。
        /// </summary>
        public int Epoch { get; init; }

        /// <summary>
        /// epoch 内单调递增的动作 id。
        /// </summary>
        public long ActionId { get; init; }

        /// <summary>
        /// 施放技能的 mob。
        /// </summary>
        public uint MobNetId { get; init; }

        /// <summary>
        /// 技能 id。
        /// </summary>
        public string SkillId { get; init; } = string.Empty;

        /// <summary>
        /// 主目标玩家 guid。
        /// </summary>
        public string TargetGuid { get; init; } = string.Empty;

        /// <summary>
        /// 主目标 NetId；0 表示无目标。
        /// </summary>
        public uint TargetNetId { get; init; }

        /// <summary>
        /// 施法朝向。
        /// </summary>
        public int AimDir { get; init; }

        /// <summary>
        /// 瞄准点 X。
        /// </summary>
        public double AimX { get; init; }

        /// <summary>
        /// 瞄准点 Y。
        /// </summary>
        public double AimY { get; init; }

        /// <summary>
        /// 计划开始 Tick。
        /// </summary>
        public long StartTick { get; init; }

        /// <summary>
        /// 蓄力/前摇结束 Tick。
        /// </summary>
        public long WindupEndTick { get; init; }

        /// <summary>
        /// 生效阶段开始 Tick。
        /// </summary>
        public long ActiveStartTick { get; init; }

        /// <summary>
        /// 生效阶段结束 Tick。
        /// </summary>
        public long ActiveEndTick { get; init; }

        /// <summary>
        /// 后摇结束 Tick。
        /// </summary>
        public long RecoveryEndTick { get; init; }

        /// <summary>
        /// 在此之前可以被打断。
        /// </summary>
        public long InterruptibleUntilTick { get; init; }

        /// <summary>
        /// 盾反/格挡的有效窗口。
        /// </summary>
        public DefenseWindow ParryWindow { get; init; }

        /// <summary>
        /// 可能产生命中的 Tick 列表，支持多段攻击。
        /// </summary>
        public IReadOnlyList<long> HitTicks { get; init; } = [];

        /// <summary>
        /// 投射物生成计划。
        /// </summary>
        public IReadOnlyList<ProjectileSpawnPlan> ProjectileSpawns { get; init; } = [];

        /// <summary>
        /// 本次动作的随机种子，用于本地表现/预测。
        /// </summary>
        public int Seed { get; init; }

        /// <summary>
        /// 技能发生时所在关卡。
        /// </summary>
        public string LevelId { get; init; } = string.Empty;

        /// <summary>
        /// 技能发生时所在子关卡。
        /// </summary>
        public int SubLevelIndex { get; init; } = -1;

        /// <summary>
        /// 服务端广播时间。
        /// </summary>
        public double ServerTimeMs { get; init; }
    }

    /// <summary>
    /// 投射物生成计划。
    /// </summary>
    public sealed class ProjectileSpawnPlan
    {
        /// <summary>
        /// 生成 Tick。
        /// </summary>
        public long Tick { get; init; }

        /// <summary>
        /// 投射物使用的技能 id。
        /// </summary>
        public string SkillId { get; init; } = string.Empty;

        /// <summary>
        /// 发射方向。
        /// </summary>
        public int Dir { get; init; }

        /// <summary>
        /// 发射速度。
        /// </summary>
        public double Speed { get; init; }

        /// <summary>
        /// 生成点 X。
        /// </summary>
        public double SpawnX { get; init; }

        /// <summary>
        /// 生成点 Y。
        /// </summary>
        public double SpawnY { get; init; }
    }
}
