namespace DeadCellsMultiplayerX.Client.Session.Guest;

using Microsoft.VisualStudio.Threading;
using ModCore.Events;
using StreamJsonRpc;

internal sealed partial class GuestGameSession
{
    // Public methods
    public void Dispose() => _ = this.StopAsync();

    public ValueTask DisposeAsync() => new(this.StopAsync());

    // Private methods
    private Task StopAsync()
    {
        lock (this.gate)
        {
            if (this.state >= GuestLifetimeState.Stopping)
            {
                return this.shutdownCompleted.Task;
            }

            if (this.state == GuestLifetimeState.Created)
            {
                this.startupCompleted.TrySetCanceled();
            }

            this.state = GuestLifetimeState.Stopping;
        }

        this.Uploader.MarkDisposed();
        this.Cleanup(this.lifetime.Cancel, "session cancellation");
        this.Cleanup(() => this.channel?.Dispose(), "server RPC close");
        _ = this.FinishStopAsync();
        return this.shutdownCompleted.Task;
    }

    private async Task FinishStopAsync()
    {
        try
        {
            await this.gameContext;
            this.levelLifecycle.Stop();
            this.ClearLevelEntrySubscription();

            // Startup may have published the channel after the first stop request.
            // Close it before joining producers so a pending RPC can finish.
            var startingChannel = this.channel;
            this.Cleanup(() => startingChannel?.Dispose(), "server RPC close");
            await this.ObserveShutdownAsync(this.startupCompleted.Task);
            if (!ReferenceEquals(startingChannel, this.channel))
            {
                this.Cleanup(() => this.channel?.Dispose(), "late server RPC close");
            }
            if (this.initialLoadTask != null)
            {
                await this.ObserveShutdownAsync(this.initialLoadTask);
            }
            if (this.levelVoteTask != null)
            {
                await this.ObserveShutdownAsync(this.levelVoteTask);
            }
            // Native constructors/destructors can reenter shutdown. Keep their hooks
            // installed until the in-progress ownership operation has unwound.
            if (this.worldCreationCompleted != null)
            {
                await this.worldCreationCompleted.Task;
            }
            if (this.worldReleaseCompleted != null)
            {
                await this.worldReleaseCompleted.Task;
            }

            await this.gameContext;

            // Resource-producing startup/loading has finished. Nothing can install
            // another event receiver, hook or world after this cleanup pass.
            if (this.eventsRegistered)
            {
                this.Cleanup(() => EventSystem.RemoveReceiver(this), "game events");
                this.eventsRegistered = false;
            }

            this.Cleanup(this.localActions.Dispose, "action recorder");
            this.Cleanup(this.ReleaseWorld, "replicated world");

            // World teardown still needs the mob/death hooks and their world lookup.
            this.Cleanup(() => this.sessionHooks?.Dispose(), "session hooks");
            this.sessionHooks = null;
            this.Cleanup(() => this.gameHookAdapter?.Dispose(), "game signal");
            this.gameHookAdapter = null;

            this.Cleanup(this.Combat.ClearDeathPermissions, "death permissions");
            this.Cleanup(this.Uploader.Clear, "upload buffer");

            this.Cleanup(() => this.debugHud?.Dispose(), "debug HUD");
            this.debugHud = null;

            await this.CleanupAsync(this.Clock.CompleteAsync, "clock sample");
            await this.CleanupAsync(this.Uploader.CompleteAsync, "frame upload");

            var channel = this.channel;
            if (channel != null)
            {
                await this.CleanupAsync(() => channel.DisposeAsync().AsTask(), "server RPC shutdown");
            }
            else
            {
                this.Cleanup(this.serverStream.Dispose, "unclaimed server stream");
            }

            this.Server = null;
            this.saveData = null;
            this.initialGame = null;
            this.channel = null;
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest game shutdown failed");
        }
        finally
        {
            this.Cleanup(this.lifetime.Dispose, "session cancellation source");

            lock (this.gate)
            {
                this.state = GuestLifetimeState.Stopped;
            }

            this.shutdownCompleted.TrySetResult();
        }
    }

    private async Task ObserveShutdownAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            this.Logger.Debug(ex, "Guest operation ended during shutdown");
        }
    }

    private void Cleanup(Action cleanup, string resource)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest cleanup failed: {Resource}", resource);
        }
    }

    private async Task CleanupAsync(Func<Task> cleanup, string resource)
    {
        try
        {
            await cleanup();
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest cleanup failed: {Resource}", resource);
        }
    }

    private void OnDisconnected(object? sender, JsonRpcDisconnectedEventArgs e)
    {
        if (e.Reason == DisconnectedReason.LocallyDisposed || this.IsDisposed)
        {
            return;
        }

        this.Logger.Warning(
            e.Exception,
            "Guest server disconnected: {Reason}: {Description}",
            e.Reason,
            e.Description);

        // The parent owns stopping; an RPC callback must never await its own dispatch completion.
        this.requestStop(true);
    }
}
