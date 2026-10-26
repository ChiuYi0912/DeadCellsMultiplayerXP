using dc.pr;
using DeadCellsMultiplayerX.Common.Data.Ghosts;
using DeadCellsMultiplayerX.Common.Data.Snapshot;
using DeadCellsMultiplayerX.Common.Ghosts;
using DeadCellsMultiplayerX.Server.WorldX.Ghosts.World;

namespace DeadCellsMultiplayerX.Server.WorldX.Ghosts.Pipeline
{
    /// <summary>
    /// 一次 Ghost 动作从准入到广播的上下文
    /// </summary>
    internal sealed class ActionContext
    {
        public ActionContext(
            string senderGuid,
            HeroAction? request,
            long serverTimeMs)
        {
            SenderGuid = senderGuid;
            Request = request;
            ServerTimeMs = serverTimeMs;
        }

        /// <summary>
        /// 动作发送者
        /// </summary>
        public string SenderGuid { get; }

        /// <summary>
        /// 客户端上报的原始动作
        /// </summary>
        public HeroAction? Request { get; }

        /// <summary>
        /// 服务端接收时间
        /// </summary>
        public long ServerTimeMs { get; }

        /// <summary>
        /// 准入阶段解析出的游戏实例
        /// </summary>
        public Game? Game { get; set; }

        /// <summary>
        /// 发送者最新的英雄快照
        /// </summary>
        public HeroDynamic? Snapshot { get; set; }

        /// <summary>
        /// 快照对应的服务端关卡
        /// </summary>
        public Level? Level { get; set; }

        /// <summary>
        /// 快照关卡 id
        /// </summary>
        public string LevelId { get; set; } = string.Empty;

        /// <summary>
        /// 快照子关卡索引
        /// </summary>
        public int SubLevelIndex { get; set; }

        /// <summary>
        /// 发送者对应的 Ghost 聚合
        /// </summary>
        public Actor? Actor { get; set; }

        /// <summary>
        /// 当前 GhostHero
        /// </summary>
        public GhostHero? Ghost => Actor?.Ghost;

        /// <summary>
        /// 技能释放进度
        /// </summary>
        public double Ratio { get; set; } = 1.0;
    }
}
