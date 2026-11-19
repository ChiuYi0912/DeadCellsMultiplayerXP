namespace DeadCellsMultiplayerX.Composition;

using System.Diagnostics;
using DeadCellsMultiplayerX.Foundation.Processes;
using DeadCellsMultiplayerX.Server;
using ModCore.Utilities;

/// <summary>
/// Composition 层的服务端 Worker 启动实现。
/// 只有这里知道 ServerRuntime 的完整类型名/返回入口，Client 不直接依赖服务端实现。
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
        {
            startInfo.Environment[key] = value;
        }

        return WorkerProcessUtils.StartWorkerProcess(
            typeof(ServerRuntime).AssemblyQualifiedName!,
            nameof(ServerRuntime.Entrypoint),
            startInfo,
            typeof(ServerRuntime).Assembly.Location);
    }
}
