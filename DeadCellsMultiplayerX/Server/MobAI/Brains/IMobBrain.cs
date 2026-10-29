using DeadCellsMultiplayerX.Common.MobAI.Contracts;

namespace DeadCellsMultiplayerX.Server.MobAI.Brains
{
    /// <summary>
    /// 服务端 Mob 决策接口。
    /// 只输出意图，不直接调用技能、动画或网络。
    /// </summary>
    public interface IMobBrain
    {
        /// <summary>
        /// 根据当前上下文产生一个意图；返回 null 表示本 Tick 不行动。
        /// </summary>
        MobIntent? Think(MobBrainContext context);
    }
}
