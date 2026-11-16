namespace DeadCellsMultiplayerX.Client.Session.Guest;

using dc.cine;
using dc.en;
using dc.tool.atk;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.World;
using DeadCellsMultiplayerX.Contracts.Snapshots;
using ModCore.Events.Interfaces.Game;
using ModCore.Utilities;
using Nerdbank.MessagePack;
using PolyType;
using PolyType.SourceGenerator;
using Microsoft.VisualStudio.Threading;

internal sealed partial class GuestGameSession
{
    private static readonly MessagePackSerializer FrameSizeSerializer = new();
    public Task EnterNewLevel(byte[] data)
    {
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.state >= GuestLifetimeState.Stopping, this);
            // A repeated callback shares the initial load; it cannot launch a second game.
            return this.loadTask ??= this.LoadGameAsync(data);
        }
    }

    private async Task LoadGameAsync(byte[] data)
    {
        try
        {
            await this.gameContext;
            this.lifetime.Token.ThrowIfCancellationRequested();
            this.saveData = data;
            this.loadedGame = await this.gameLoader.LoadSavedGameAsync(this.lifetime.Token);
            this.TryCreateWorld();
        }
        catch (Exception ex)
        {
            this.initialWorld.TrySetCanceled();
            if (!this.IsDisposed)
            {
                this.Logger.Error(ex, "Guest game loading failed");
                this.requestStop(true);
            }
            throw;
        }
    }

    /// <summary>
    /// 服务端调用,用于通知当前客户切换关卡
    /// </summary>
    /// <param name="levelid"></param>
    /// <returns></returns>
    public Task EnterNextLevel(string levelid)
    {
        ObjectDisposedException.ThrowIf(this.IsDisposed, this);
        this.Logger.Information("Entering next level: {LevelId}", levelid);
        this.serverDrivenTransition = true;
        try
        {
            LevelTransition.Class.@goto.Invoke(levelid.AsHaxeString());
        }
        finally
        {
            this.serverDrivenTransition = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 服务端每 tick 推送完整世界帧
    /// </summary>
    public Task SyncWorldFrame(WorldFrame frame)
    {
        if (frame == null || !this.protocolValidated || this.IsDisposed)
        {
            return Task.CompletedTask;
        }

#if DEBUG
        if (WorldFrameDebug.ConsumeDropServerFrame())
        {
            return Task.CompletedTask;
        }

        if (DebugFlags.Get("netstat-Measure"))
        {
            this.MeasureWorldFrame(frame);
        }
#endif

        this.ClientWorld?.OnWorldFrame(frame);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 仅由时间线交付的帧确认命令；普通推送与补发共用此入口。
    /// </summary>
    internal void AcknowledgeCommands(WorldFrame frame)
        => this.localCapture?.Commands.Acknowledge(frame, this.LocalGuid);

    /// <summary>
    /// 服务端请求补发本地上传帧
    /// </summary>
    public Task<ClientFrameBundle> RequestClientFrames(ClientFrameRangeRequest request)
        => Task.FromResult(this.IsDisposed ? new ClientFrameBundle { Unavailable = true }
            : this.Uploader.BuildBundle(request));

    internal void ObserveWorldEpoch(int epoch)
    {
        if (this.IsDisposed)
        {
            return;
        }

        this.Uploader.SetEpoch(epoch);
        this.localCapture?.ObserveEpoch(epoch);
    }

    internal void ObserveServerTime(long serverTimeMs) => this.Clock.ObserveServerTime(serverTimeMs);

    /// <summary>
    /// 播放远端 mob 的受击表现，避免 Mob.onDamage 内部递归
    /// </summary>
    internal void PlayHitVisual(AttackData atk, Mob mob)
    {
        if (mob == null || mob.destroyed || this.replayVisualDepth != 0)
        {
            return;
        }

        this.Logger.Verbose(
            "Puppet hit visual: target={Target} dmg={Dmg} hit={Hit}",
            mob.type, atk.finalDmg, atk.hitResult);

        this.replayVisualDepth++;
        try
        {
            mob.onDamage(atk);
        }
        finally
        {
            this.replayVisualDepth--;
        }
    }

    /// <summary>
    /// 强制客户端 mob 执行原生死亡
    /// </summary>
    internal void ForceNativeDeath(Mob mob)
    {
        if (mob == null || mob.destroyed)
        {
            return;
        }

        using (this.DeathGate.Allow(mob))
        {
            mob.life = 0;
            mob.onDie();
        }

        if (!mob.destroyed)
        {
            mob.destroy();
        }
    }

    internal void DebugCastSkill(string itemId, int slot)
        => this.localCapture?.DebugCastSkill(itemId, slot);

    internal void DebugUseWeapon(string itemId, int slot, int cycle = 0)
        => this.localCapture?.DebugUseWeapon(itemId, slot, cycle);

    void IOnFrameUpdate.OnFrameUpdate(double dt)
    {
        if (!this.protocolValidated || this.IsDisposed)
        {
            return;
        }

        // 同步 TimeStamp
        this.TickTimeSystem();

        // 世界帧 NACK 调度/超时
        this.ClientWorld?.Tick(this.SyncedTimeMs);
    }

    void IOnLocalHeroPostUpdate.OnPostUpdate(Hero hero)
    {
        if (!this.IsDisposed)
        {
            this.Uploader.CaptureAndUpload(hero);
        }
    }

    void IOnLocalHeroInitDone.OnHeroInitDone(Hero hero)
    {
        if (!this.IsDisposed)
        {
            this.Uploader.ResetLocalHeroState();
        }
    }

    private void TryCreateWorld()
    {
        lock (this.gate)
        {
            if (this.state >= GuestLifetimeState.Stopping)
            {
                throw new OperationCanceledException(this.lifetime.Token);
            }

            if (!this.protocolValidated || this.loadedGame == null || this.ClientWorld != null)
            {
                return;
            }

            if (this.loadedGame != Game)
            {
                throw new InvalidOperationException("Guest game was replaced during initialization.");
            }

            this.DeathGate.Clear();
            this.ClientWorld = new ClientWorld(this, this.LocalGuid);
            this.Uploader.ResetLocalHeroState();
            this.initialWorld.TrySetResult();
        }
    }

    private void TickTimeSystem() => this.Clock.Tick();

    private void MeasureWorldFrame(WorldFrame frame)
    {
        var shape = TypeShapeProvider_DeadCellsMultiplayerX.Default
            .GetTypeShapeOrThrow<WorldFrame>();

        this.LastWorldFrameBytes = FrameSizeSerializer
            .Serialize(frame, shape)
            .Length;
    }

}
