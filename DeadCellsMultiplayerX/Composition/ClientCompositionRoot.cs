namespace DeadCellsMultiplayerX.Composition;

using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Foundation.Processes;

/// <summary>
/// 客户端进程的组合入口。
/// 只负责创建 ServerWorkerProcessLauncher 并注入 ClientRuntime；不承载客户端业务逻辑。
/// </summary>
internal static class ClientCompositionRoot
{
    public static void Start()
    {
        IServerProcessLauncher launcher = new ServerWorkerProcessLauncher();
        new ClientRuntime().Init(launcher);
    }
}
