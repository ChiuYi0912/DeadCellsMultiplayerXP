namespace DeadCellsMultiplayerX.Client.UI.Lobby.Controllers;

using dc.ui;
using DeadCellsMultiplayerX.Client.Session;
using DeadCellsMultiplayerX.Client.UI.Common;

//Controller 基类
internal abstract class LobbyController(
    ClientRuntime client,
    LobbyContext ctx,
    LobbyStateMachine state,
    Process? parent = null) : UIProcess(parent)
{
    protected ClientRuntime Client { get; } = client;
    protected LobbyContext Ctx { get; } = ctx;
    protected LobbyStateMachine State { get; } = state;

    //错误处理
    protected void Fail(Exception ex, string msg)
    {
        this.Logger.Error(ex, msg);
        this.Ctx.LastError = $"{msg}: {ex.Message}";

        if (this.State.CanGo(LobbyState.Error))
        {
            this.State.TryGo(LobbyState.Error);
        }
    }

    //错误提示
    protected void Fail(string msg)
    {
        this.Logger.Error(msg);
        this.Ctx.LastError = msg;
    }

    protected async Task RunAsync(Func<CancellationToken, Task> action, string operationName)
    {
        try
        {
            await action(this.DisposeToken);
        }
        catch (OperationCanceledException) when (this.IsDisposed)
        {
            this.Logger.Information($"{operationName} canceled");
        }
        catch (Exception ex)
        {
            this.Fail(ex, operationName);
        }
    }
}
