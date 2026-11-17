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
                return this.stopped.Task;
            }

            if (this.state == GuestLifetimeState.Created)
            {
                this.started.TrySetCanceled();
            }

            this.state = GuestLifetimeState.Stopping;
        }

        this.lifetime.Cancel();
        this.Uploader.MarkDisposed();
        this.channel?.Dispose();
        _ = this.FinishStopAsync();
        return this.stopped.Task;
    }

    private async Task FinishStopAsync()
    {
        try
        {
            await this.gameContext;

            if (this.eventsRegistered)
            {
                this.Cleanup(() => EventSystem.RemoveReceiver(this), "game events");
                this.eventsRegistered = false;
            }

            this.Cleanup(() => this.hooks?.Dispose(), "session hooks");
            this.Cleanup(() => this.gameHooks?.Dispose(), "game signal");
            this.hooks = null;
            this.gameHooks = null;

            this.Cleanup(this.localActions.Dispose, "action recorder");
            this.Cleanup(() => this.ClientWorld?.Dispose(), "replicated world");
            this.ClientWorld = null;

            this.DeathGate.Clear();
            this.Uploader.Clear();

            this.Cleanup(() => this.debugHud?.Dispose(), "debug HUD");
            this.debugHud = null;

            // Each producer sees cancellation/closed RPC before we join it.
            await this.ObserveShutdownAsync(this.started.Task);

            if (this.loadTask != null)
            {
                await this.ObserveShutdownAsync(this.loadTask);
            }

            await this.Clock.CompleteAsync();
            await this.Uploader.CompleteAsync();

            if (this.channel != null)
            {
                await this.channel.DisposeAsync();
            }
            else
            {
                this.Cleanup(this.serverStream.Dispose, "unclaimed server stream");
            }

            this.Server = null;
            this.saveData = null;
            this.loadedGame = null;
            this.channel = null;
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "Guest game shutdown failed");
        }
        finally
        {
            this.lifetime.Dispose();

            lock (this.gate)
            {
                this.state = GuestLifetimeState.Stopped;
            }

            this.stopped.TrySetResult();
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
