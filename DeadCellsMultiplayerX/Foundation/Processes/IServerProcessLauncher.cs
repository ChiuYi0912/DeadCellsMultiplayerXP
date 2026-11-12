namespace DeadCellsMultiplayerX.Foundation.Processes;

using System.Diagnostics;

/// <summary>
/// 服务端 Worker 进程启动契约。
/// Client 只依赖本接口；具体 ServerMain 入口描述由 Composition 提供。
/// </summary>
internal interface IServerProcessLauncher
{
    /// <summary>
    /// 启动服务端 Worker 进程。
    /// </summary>
    public Process Start(ServerProcessOptions options);
}
