namespace DeadCellsMultiplayerX.Common.MobAI.Contracts
{
    /// <summary>
    /// Mob Brain 的运行模式。
    /// 服务端是唯一权威决策者；客户端只用于预测和表现。
    /// </summary>
    public enum MobBrainMode
    {
        /// <summary>
        /// 服务端权威模拟。
        /// </summary>
        ServerAuthoritative = 0,

        /// <summary>
        /// 客户端只读预测，不修改权威状态。
        /// </summary>
        ClientPrediction = 1,
    }
}
