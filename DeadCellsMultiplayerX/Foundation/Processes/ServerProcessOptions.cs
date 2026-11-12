namespace DeadCellsMultiplayerX.Foundation.Processes
{
    /// <summary>
    /// 启动服务端 Worker 进程所需的端侧配置。
    /// 只描述“怎么启动”，不包含 ServerMain 或任何服务端实现类型。
    /// </summary>
    internal sealed class ServerProcessOptions
    {
        /// <summary>
        /// 传给 Worker 进程的环境变量（管道句柄等）。
        /// </summary>
        public Dictionary<string, string> EnvironmentVariables { get; } = [];

        /// <summary>
        /// 是否重定向标准输出。
        /// </summary>
        public bool RedirectStandardOutput { get; init; }

        /// <summary>
        /// 是否重定向标准错误。
        /// </summary>
        public bool RedirectStandardError { get; init; }
    }
}
