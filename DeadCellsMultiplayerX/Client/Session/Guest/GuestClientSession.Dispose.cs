namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
using StreamJsonRpc;

internal sealed partial class GuestClientSession
{
    protected override void MyDispose()
    {
        base.MyDispose();

        this.prediction?.Clear();
        this.deathGate?.Clear();

        // Stop inbound RPC before tearing down game-facing state; otherwise a
        // frame can land in a half-disposed world.
        this.DisposeSafely(this.rpc, "Guest server RPC");
        this.rpc = null;
        this.DisposeSafely(serverStream, "Guest server stream");

        // Disable session hooks before the world they call into disappears.
        this.DisposeSafely(this.hookSession, "Guest hook session");
        this.hookSession = null;

        this.DisposeSafely(this.localCapture, "Local action capture");
        this.localCapture = null;

        this.DisposeSafely(this.ClientWorld, "Client world");
        this.ClientWorld = null;

        this.prediction = null;
        this.deathGate = null;
        this.Uploader.Clear();

        DebugHUD.DisposeInstance();
    }

    private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed)
        {
            return;
        }

        this.Logger.Error(
            e.Exception,
            "Abort connection: {reason}: {desc}",
            e.Reason,
            e.Description);

        ModCore.Modules.Game.SynchronizationContext.Post(static _ =>
        {
            ClientRuntime.Instance.CleanupClient();

            Boot.Class.ME.returnToMainMenu();
        }, null);

        this.Dispose();
    }
}
