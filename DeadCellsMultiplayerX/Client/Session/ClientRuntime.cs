namespace DeadCellsMultiplayerX.Client.Session;

using DeadCellsMultiplayerX.Diagnostics;
using dc.en;
using dc.pr;
using dc.shader;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.Session.Host;
using DeadCellsMultiplayerX.Client.UI;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Foundation.Processes;
using DeadCellsMultiplayerX.Networking.Transport.Quic;
using Hashlink.Virtuals;
using HaxeProxy.Runtime;
using ModCore;
using ModCore.Events;
using ModCore.Utilities;
using Microsoft.VisualStudio.Threading;

internal sealed partial class ClientRuntime : Module<ClientRuntime>, IClientLifecycleHookSink
{
    /// <summary>
    /// 服务端 Worker 启动器；由 Composition 注入
    /// </summary>
    private IServerProcessLauncher? serverProcessLauncher;

    /// <summary>
    /// 进程级 Hook 的租约
    /// </summary>
    private ClientHookBootstrap? hookBootstrap;

    private readonly SemaphoreSlim startGate = new(1, 1);
    private CancellationTokenSource? pendingStart;
    private long startGeneration;

    /// <summary>
    /// 当前的 Guest Client 实例
    /// </summary>
    public GuestClient? CurrentGuestClient { get; internal set; }

    /// <summary>
    /// 当前的 Host Client 实例
    /// </summary>
    public HostClient? CurrentHostClient { get; internal set; }

    /// <summary>
    /// 连接大厅
    /// </summary>
    public LobbyMenuX? LobbyMenu { get; internal set; }

    //初始化客户端
    public void Init(IServerProcessLauncher launcher)
    {
        this.serverProcessLauncher = launcher;

        this.hookBootstrap = new ClientHookBootstrap(this);
        AppDomain.CurrentDomain.ProcessExit += this.OnProcessExit;
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        this.hookBootstrap?.Dispose();
        this.hookBootstrap = null;
    }

    private static void Hook_Hero_postUpdate(Hook_Hero.orig_postUpdate orig, Hero self)
    {
        orig(self);
        EventSystem.BroadcastEvent<IOnLocalHeroPostUpdate, Hero>(self);
    }

    private static void Hook_TitleScreen_onDispose(Hook_TitleScreen.orig_onDispose orig, TitleScreen self)
    {
        orig(self);
        EventSystem.BroadcastEvent<IOnLobbyMenuDisposed>();
    }

    private static void Hook_GlowKey_applyGlowData(Hook_GlowKey.orig_applyGlowData orig, GlowKey self,
        int i, virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData)
    {
        if (self.colorsCount__ <= i)
        {
            self.colorsCount__ = i + 1;
            self.constModified = true;
        }

        orig(self, i, glowData);
    }

    public void CleanupClient()
    {
        this.pendingStart?.Cancel();
        var guest = this.CurrentGuestClient;
        var host = this.CurrentHostClient;
        guest?.Dispose();
        _ = this.CleanupClientsAsync(guest, host);
    }

    private async Task CleanupClientsAsync(GuestClient? guest, HostClient? host)
    {
        try
        {
            if (guest != null)
            {
                await guest.DisposeAsync();
            }

            await ModCore.Modules.Game.SynchronizationContext;

            if (ReferenceEquals(this.CurrentGuestClient, guest))
            {
                this.CurrentGuestClient = null;
            }

            this.DisposeHost(host);
        }
        catch (Exception ex) { Logger.Warning(ex, "Client cleanup failed"); }
    }

    private void DisposeHost(HostClient? host)
    {
        try
        {
            host?.Dispose();
        }
        catch (Exception ex) { Logger.Warning(ex, "Host cleanup failed"); }
        finally
        {
            if (ReferenceEquals(this.CurrentHostClient, host))
            {
                this.CurrentHostClient = null;
            }
        }
    }

    public async Task StartHost(string ip, int port, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this.startGeneration);
        await this.startGate.WaitAsync(cancellationToken);
        using var start = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.pendingStart = start;
        try
        {
            await this.CleanupClientsAsync(this.CurrentGuestClient, this.CurrentHostClient);
            await ModCore.Modules.Game.SynchronizationContext;
            start.Token.ThrowIfCancellationRequested();
            var launcher = this.serverProcessLauncher
                ?? throw new InvalidOperationException("ClientRuntime.Init requires a server process launcher.");
            var listener = new QuicNetworkListener(ip, port);
            try
            {
                await listener.Init();
                start.Token.ThrowIfCancellationRequested();
                this.CurrentHostClient = new HostClient(listener, default, launcher);
            }
            catch
            {
                await listener.DisposeAsync();
                throw;
            }
            await this.CurrentHostClient.Init();
            start.Token.ThrowIfCancellationRequested();
            await this.StartGuestCoreAsync(ip, port, null, start.Token);
        }
        catch
        {
            await this.CleanupClientsAsync(this.CurrentGuestClient, this.CurrentHostClient);
            throw;
        }
        finally
        {
            this.pendingStart = null;
            this.startGate.Release();
        }
    }

    public async Task StartGuest(string ip, int port, string? playerName = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref this.startGeneration);
        await this.startGate.WaitAsync(cancellationToken);
        using var start = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        this.pendingStart = start;
        try
        { await this.StartGuestCoreAsync(ip, port, playerName, start.Token); }
        finally
        {
            this.pendingStart = null;
            this.startGate.Release();
        }
    }

    private async Task StartGuestCoreAsync(string ip, int port, string? playerName,
        CancellationToken cancellationToken)
    {
        await ModCore.Modules.Game.SynchronizationContext;
        if (this.CurrentGuestClient is { } previous)
        {
            if (!previous.IsDisposed)
            {
                throw new InvalidOperationException("A guest is already connected.");
            }
            // Finish the old room's cleanup before clearing its identity; its
            // queued stop notification may lose the race to this reconnect.
            await this.CleanupClientsAsync(previous, this.CurrentHostClient);
            await ModCore.Modules.Game.SynchronizationContext;
        }
        var connection = await QuicNetworkConnect.Connect(ip, port, cancellationToken);
        GuestClient? guest = null;
        try
        {
            await ModCore.Modules.Game.SynchronizationContext;
            cancellationToken.ThrowIfCancellationRequested();
            guest = new GuestClient(connection, ModCore.Modules.Game.SynchronizationContext);
            guest.Stopping += this.OnGuestStopping;
            this.CurrentGuestClient = guest;
            await guest.JoinLobbyAsync(string.IsNullOrEmpty(playerName) ? Environment.UserName : playerName,
                cancellationToken);
        }
        catch
        {
            if (guest != null)
            {
                await guest.DisposeAsync();
            }
            else
            {
                await connection.DisposeAsync();
            }

            await ModCore.Modules.Game.SynchronizationContext;

            if (ReferenceEquals(this.CurrentGuestClient, guest))
            {
                this.CurrentGuestClient = null;
            }

            throw;
        }
    }

    private void OnGuestStopping(GuestClient guest, bool returnToMenu)
        => _ = this.HandleGuestStoppingAsync(guest, returnToMenu, this.startGeneration);

    private async Task HandleGuestStoppingAsync(GuestClient guest, bool returnToMenu, long generation)
    {
        try
        {
            await guest.DisposeAsync();
            await ModCore.Modules.Game.SynchronizationContext;
            // A late callback from an old connection must never clean up a new one.
            if (!ReferenceEquals(this.CurrentGuestClient, guest))
            {
                return;
            }

            this.CurrentGuestClient = null;
            this.DisposeHost(this.CurrentHostClient);

            if (returnToMenu && generation == this.startGeneration)
            {
                dc.Boot.Class.ME.returnToMainMenu();
            }
        }
        catch (Exception ex) { Logger.Warning(ex, "Guest stop notification failed"); }
    }

    #region Menu

    private static void Hook__TitleScreen__constructor__(Hook__TitleScreen.orig___constructor__ orig, TitleScreen arg1, bool? playMusic) => orig(arg1, playMusic);

    private void Hook_TitleScreen_mainMenu(
        Hook_TitleScreen.orig_mainMenu orig, TitleScreen self)
    {
        orig(self);

        const int index = 1;

        var color = (255 << 16) | (215 << 8) | 0;
        BuildMenuChild("Online", this.OpenLobby, color: color);

        var wrapper = self.menuItemsWrapper;
        var menu = wrapper.children.getDyn(wrapper.children.length - 1);
        wrapper.removeChild(menu);
        wrapper.addChildAt(menu, index);

        var item = self.menuItems.pop();
        self.menuItems.insert(index, item);

        self.fControlLabel.reflow();
        self.select(0, default);

#if DEBUG
        // DebugLauncher is compiled only under DEBUG (see its file-level #if).
        DebugLauncher.TriggerAutoJoin();
#endif
    }

    private void OpenLobby()
    {
        if (this.LobbyMenu != null && !this.LobbyMenu.IsDisposed)
        {
            return;
        }

        this.LobbyMenu = new LobbyMenuX(
            client: this,
            titleScreen: TitleScreen.Class.ME
        );
    }

    public static virtual_cb_help_inter_isEnable_t_<bool> BuildMenuChild(
    string text, HlAction cb, string? help = null, bool? isEnable = null, int color = 0xFFFFFF) => TitleScreen.Class.ME.addMenu(text.AsHaxeString(), cb,
            help?.AsHaxeString(), isEnable, Ref<int>.From(ref color));
    #endregion
}
