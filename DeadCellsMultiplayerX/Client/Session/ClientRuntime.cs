using dc.shader;
using dc.pr;
using DeadCellsMultiplayerX.Client.Session.Guest;
using DeadCellsMultiplayerX.Client.Session.Host;
using DeadCellsMultiplayerX.Networking.Transport.Quic;
using HaxeProxy.Runtime;
using ModCore;
using DeadCellsMultiplayerX.Client.UI;
using ModCore.Events;
using dc.en;
using DeadCellsMultiplayerX.Client.Integration.Events;
using DeadCellsMultiplayerX.Client.Integration.Hooks;
using DeadCellsMultiplayerX.Engine.Hooks.Client;
using DeadCellsMultiplayerX.Foundation.Processes;
using Hashlink.Virtuals;
using ModCore.Utilities;
namespace DeadCellsMultiplayerX.Client.Session
{
    internal partial class ClientRuntime : Module<ClientRuntime>, IClientLifecycleHookSink
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
        public LobbyMenuX? lobbyMenu { get; internal set; }

        //初始化客户端
        public void Init(IServerProcessLauncher launcher)
        {
            serverProcessLauncher = launcher;

            hookBootstrap = new ClientHookBootstrap(this);
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        }

        private void OnProcessExit(object? sender, EventArgs e)
        {
            hookBootstrap?.Dispose();
            hookBootstrap = null;
        }

        private void Hook_Hero_postUpdate(Hook_Hero.orig_postUpdate orig, Hero self)
        {
            orig(self);
            EventSystem.BroadcastEvent<IOnLocalHeroPostUpdate, Hero>(self);
        }

        private void Hook_TitleScreen_onDispose(Hook_TitleScreen.orig_onDispose orig, TitleScreen self)
        {
            orig(self);
            EventSystem.BroadcastEvent<IOnLobbyMenuDisposed>();
        }

        private void Hook_GlowKey_applyGlowData(Hook_GlowKey.orig_applyGlowData orig, GlowKey self,
            int i, Hashlink.Virtuals.virtual_animationIntensity_animationScale_animationSpeed_animationTextureMask_inner_key_outer_power_ glowData)
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

            CurrentGuestClient?.Dispose();
            CurrentHostClient?.Dispose();
            CurrentHostClient = null;
            CurrentGuestClient = null;

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
            CleanupClient();

            var listener = new QuicNetworkListener(ip, port);

            await listener.Init();

            var launcher = serverProcessLauncher
                ?? throw new InvalidOperationException(
                    "ClientRuntime.Init must be called with an IServerProcessLauncher.");

            CurrentHostClient = new HostClient(listener, default, launcher);
            await CurrentHostClient.Init();

            await StartGuest(ip, port);
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

            if (CurrentGuestClient != null && !CurrentGuestClient.IsDisposed)
            {
                throw new InvalidOperationException();
            }

            var connect = await QuicNetworkConnect.Connect(ip, port, default);

            CurrentGuestClient = new GuestClient(connect);
            await CurrentGuestClient.Init(playerName);
        }

        #region Menu

        private void Hook__TitleScreen__constructor__(Hook__TitleScreen.orig___constructor__ orig, TitleScreen arg1, bool? playMusic)
        {
            orig(arg1, playMusic);

        }

        private void Hook_TitleScreen_mainMenu(
            Hook_TitleScreen.orig_mainMenu orig, TitleScreen self)
        {
            orig(self);

            const int Index = 1;

            int color = (255 << 16) | (215 << 8) | 0;
            BuildMenuChild("Online", () => OpenLobby(), color: color);

            var wrapper = self.menuItemsWrapper;
            var menu = wrapper.children.getDyn(wrapper.children.length - 1);
            wrapper.removeChild(menu);
            wrapper.addChildAt(menu, Index);

            var item = self.menuItems.pop();
            self.menuItems.insert(Index, item);

            self.fControlLabel.reflow();
            self.select(0, default);

#if DEBUG
            // DebugLauncher is compiled only under DEBUG (see its file-level #if).
            DebugLauncher.TriggerAutoJoin();
#endif
        }

        private void OpenLobby()
        {
            if (lobbyMenu != null && !lobbyMenu.IsDisposed) return;

            lobbyMenu = new LobbyMenuX(
                client: this,
                titleScreen: TitleScreen.Class.ME
            );
        }

        public virtual_cb_help_inter_isEnable_t_<bool> BuildMenuChild(
        string text, HlAction cb, string? help = null, bool? isEnable = null, int color = 0xFFFFFF)
        {
            return TitleScreen.Class.ME.addMenu(text.AsHaxeString(), cb,
                help?.AsHaxeString(), isEnable, Ref<int>.From(ref color));
        }
        #endregion
    }
}
