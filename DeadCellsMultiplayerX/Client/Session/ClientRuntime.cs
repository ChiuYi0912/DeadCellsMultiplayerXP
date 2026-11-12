namespace DeadCellsMultiplayerX.Client.Session;

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

internal sealed partial class ClientRuntime : Module<ClientRuntime>, IClientLifecycleHookSink
{
    /// <summary>
    /// 服务端 Worker 启动器；由 Composition 注入。
    /// </summary>
    private IServerProcessLauncher? serverProcessLauncher;

    /// <summary>
    /// 进程级 Hook 的租约。
    /// </summary>
    private ClientHookBootstrap? hookBootstrap;

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

    /// <summary>
    /// 清理 Client 实例
    /// </summary>
    public void CleanupClient()
    {
        Logger.Information("Cleaning clients...");

        this.CurrentGuestClient?.Dispose();
        this.CurrentHostClient?.Dispose();
        this.CurrentHostClient = null;
        this.CurrentGuestClient = null;

        GC.Collect(2, GCCollectionMode.Forced);

        Task.Delay(200).ContinueWith(_ => GC.Collect(2, GCCollectionMode.Forced));
    }

    /// <summary>
    /// 启动 Host 客户端，并加入房间
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    public async Task StartHost(string ip, int port)
    {
        this.CleanupClient();

        var listener = new QuicNetworkListener(ip, port);

        await listener.Init();

        var launcher = this.serverProcessLauncher
            ?? throw new InvalidOperationException(
                "ClientRuntime.Init must be called with an IServerProcessLauncher.");

        this.CurrentHostClient = new HostClient(listener, default, launcher);
        await this.CurrentHostClient.Init();

        await this.StartGuest(ip, port);
    }

    /// <summary>
    /// 启动 Guest 客户端
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public async Task StartGuest(string ip, int port, string? playerName = null)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            playerName = Environment.UserName;
        }

        if (this.CurrentGuestClient != null && !this.CurrentGuestClient.IsDisposed)
        {
            throw new InvalidOperationException();
        }

        var connect = await QuicNetworkConnect.Connect(ip, port, default);

        this.CurrentGuestClient = new GuestClient(connect);
        await this.CurrentGuestClient.Init(playerName);
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
