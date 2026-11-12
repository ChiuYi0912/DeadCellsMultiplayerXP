using System.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Processes;
using DeadCellsMultiplayerX.Server.Core;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Composition
{
    /// <summary>
    /// Composition 层的服务端 Worker 启动实现。
    /// 只有这里知道 ServerMain 的完整类型名/返回入口，Client 不直接依赖服务端实现。
    /// </summary>
    internal sealed class ServerWorkerProcessLauncher : IServerProcessLauncher
    {
        public Process Start(ServerProcessOptions options)
        {
            var startInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = options.RedirectStandardOutput,
                RedirectStandardError = options.RedirectStandardError,
            };

            foreach (var (key, value) in options.EnvironmentVariables)
                startInfo.Environment[key] = value;

            return WorkerProcessUtils.StartWorkerProcess(
                typeof(ServerMain).AssemblyQualifiedName!,
                nameof(ServerMain.Entrypoint),
                startInfo,
                typeof(ServerMain).Assembly.Location);
        }
    }
}
