using dc;
using StreamJsonRpc;
using DeadCellsMultiplayerX.Client.UI.DebugTools;
namespace DeadCellsMultiplayerX.Client.Session.Guest
{
    internal partial class GuestClientSession
    {
        protected override void MyDispose()
        {
            base.MyDispose();

            prediction?.Clear();
            deathGate?.Clear();

            // Stop inbound RPC before tearing down game-facing state; otherwise a
            // frame can land in a half-disposed world.
            DisposeSafely(rpc, "Guest server RPC");
            rpc = null;
            DisposeSafely(serverStream, "Guest server stream");

            // Disable session hooks before the world they call into disappears.
            DisposeSafely(hookSession, "Guest hook session");
            hookSession = null;

            DisposeSafely(localCapture, "Local action capture");
            localCapture = null;

            DisposeSafely(remoteWorld, "Client world");
            remoteWorld = null;

            prediction = null;
            deathGate = null;
            Uploader.Clear();

            DebugHUD.DisposeInstance();
        }

        private void Rpc_Disconnected(object? sender, JsonRpcDisconnectedEventArgs e)
        {
            if (e.Reason == DisconnectedReason.LocallyDisposed)
            {
                return;
            }

            Logger.Error(
                e.Exception,
                "Abort connection: {reason}: {desc}",
                e.Reason,
                e.Description);

            ModCore.Modules.Game.SynchronizationContext.Post(static _ =>
            {
                ClientRuntime.Instance.CleanupClient();

                Boot.Class.ME.returnToMainMenu();
            }, null);

            Dispose();
        }
    }
}
