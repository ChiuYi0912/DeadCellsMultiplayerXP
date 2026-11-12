namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using CoreLibrary.Utilities;
using dc;
using dc.h2d;
using dc.hxd;
using dc.pr;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;

internal sealed class LobbyView : UIProcess
{
    private readonly TitleScreen titleScreen;
    private readonly LobbyUI owner;
    public LobbyContext Context { get; }
    private readonly Dictionary<Flow, Action> resizers = [];

    private Flow? mainFlow;      // 全屏透明
    private Graphics? dimMask;   // 半透明遮罩
    private Flow? cardFlow;      //卡片容器

    // 子页面
    private MainMenuView? mainMenuView;
    private CreateRoomView? createRoomView;
    private JoinRoomView? joinRoomView;
    private RoomView? roomView;
    private ErrorView? errorView;
    private DebugView? debugView;

    public LobbyView(LobbyUI owner, TitleScreen titleScreen, Process? parent = null) : base(parent)
    {
        this.owner = owner;
        this.titleScreen = titleScreen;
        this.Context = owner.Context;

        OnScreenResized += this.OnLayoutResize;
    }
    public int CardW => this.ScreenW * 2 / 3;
    public int CardH => this.ScreenH * 2 / 3;

    public void Show() => this.BuildSkeleton();

    public void Hide()
    {
        this.ClearAllPages();
        this.mainFlow?.removeChildren();
        this.mainFlow?.remove();
        this.mainFlow = null;
        this.dimMask = null;
        this.cardFlow = null;
    }

    //事件转发
    public void OnGoCreateRoom() => this.owner.OnGoCreateRoom();
    public void OnGoJoinRoom() => this.owner.OnGoJoinRoom();
    public void OnBackToMain() => this.owner.OnBackToMain();

    public void OnSubmitCreateRoom(string ip, int port) => _ = this.owner.OnCreateRoom(ip, port);
    public Task OnSubmitCreateRoomAsync(string ip, int port) => this.owner.OnCreateRoom(ip, port);
    public void OnSubmitJoinRoom(string ip, int port) => _ = this.owner.OnJoinRoom(ip, port);

    public void OnClickStartGame() => _ = this.owner.OnStartGame();
    public void OnClickToggleReady() => this.owner.OnToggleReady();
    public void OnClickLeaveRoom() => this.owner.OnLeaveRoom();
    public void OnRequestClose() => this.owner.OnRequestClose(); //关闭整个 Lobby
    public void GotoDebug() => this.owner.OnGotoDebug();

    private void OnLayoutResize()
    {
        foreach (var r in this.resizers.Values)
        {
            r();
        }
    }


    //状态切换
    public void OnEnterState(LobbyState state, LobbyContext ctx)
    {
        if (this.IsDisposed)
        {
            return;
        }

        if (this.cardFlow == null)
        {
            return;
        }

        // 清空旧页面
        this.ClearAllPages();
        this.cardFlow.removeChildren();

        // 重置布局
        this.cardFlow.set_isVertical(false);
        this.cardFlow.set_horizontalAlign(new FlowAlign.Middle());
        this.cardFlow.set_verticalAlign(new FlowAlign.Middle());
        this.cardFlow.set_horizontalSpacing(this.Pixel(20));

        // 挂载新页面
        switch (state)
        {
            case LobbyState.MainMenu:
                this.mainMenuView ??= new MainMenuView(this, this);
                this.mainMenuView.Build(this.cardFlow);
                break;

            case LobbyState.CreateRoom:
                this.createRoomView ??= new CreateRoomView(this, this);
                this.createRoomView.Build(this.cardFlow);
                break;

            case LobbyState.JoinRoom:
                this.joinRoomView ??= new JoinRoomView(this, this);
                this.joinRoomView.Build(this.cardFlow);
                break;

            case LobbyState.Connecting:
                this.BuildConnectingPage(this.cardFlow);
                break;

            case LobbyState.InRoom:
            case LobbyState.StartingGame:
                this.roomView ??= new RoomView(this, this);
                this.roomView.Build(this.cardFlow);
                break;

            case LobbyState.Error:
                this.errorView ??= new ErrorView(this, this);
                this.errorView.Build(this.cardFlow, ctx.LastError);
                break;
            case LobbyState.Debug:
                this.debugView ??= new DebugView(this, this);
                this.debugView.Build(this.cardFlow);
                break;
            case LobbyState.InGame:
                break;
            default:
                break;
        }

        this.cardFlow.reflow();
        this.mainFlow?.reflow();
    }

    public void Refresh(LobbyContext ctx, LobbyState state)
    {
        if (this.IsDisposed)
        {
            return;
        }

        switch (state)
        {
            case LobbyState.MainMenu:
                this.mainMenuView?.Refresh(ctx);
                break;
            case LobbyState.CreateRoom:
                this.createRoomView?.Refresh(ctx);
                break;
            case LobbyState.JoinRoom:
                this.joinRoomView?.Refresh(ctx);
                break;
            case LobbyState.InRoom:
            case LobbyState.StartingGame:
                this.roomView?.Refresh(ctx);
                break;
            case LobbyState.Error:
                this.errorView?.Refresh(ctx);
                break;
            case LobbyState.Debug:
                this.debugView?.Refresh(ctx);
                break;
            case LobbyState.Connecting:
                break;
            case LobbyState.InGame:
                break;
            default:
                break;
        }
    }


    private void ApplyFullscreenLayout()
    {
        if (this.mainFlow == null)
        {
            return;
        }

        this.mainFlow.set_minWidth(this.ScreenW);
        this.mainFlow.set_minHeight(this.ScreenH);
        this.mainFlow.set_maxWidth(this.ScreenW);
        this.mainFlow.set_maxHeight(this.ScreenH);

        if (this.dimMask != null)
        {
            this.dimMask.clear();
            this.dimMask.beginFill(Ref<int>.In(0x000000), Ref<double>.In(0.4));
            this.dimMask.drawRect(0, 0, this.ScreenW, this.ScreenH);
            this.dimMask.endFill();
        }
    }

    private void ApplyCardFlowLayout()
    {
        if (this.cardFlow == null)
        {
            return;
        }

        var cardW = this.ScreenW * 2 / 3;
        var cardH = this.ScreenH * 2 / 3;

        this.cardFlow.set_minWidth(cardW);
        this.cardFlow.set_minHeight(cardH);
        this.cardFlow.set_maxWidth(cardW);
        this.cardFlow.set_maxHeight(cardH);

        this.cardFlow.x = (this.ScreenW - cardW) / 2;
        this.cardFlow.y = (this.ScreenH - cardH) / 2;
        this.cardFlow.posChanged = true;
    }

    //骨架
    private void BuildSkeleton()
    {
        if (this.mainFlow != null)
        {
            return;
        }

        var layers = Main.Class.ME.root;

        this.mainFlow = new Flow(null);
        this.mainFlow.set_isVertical(false);
        this.mainFlow.posChanged = true;

        layers.addChildAt(this.mainFlow, Const.Class.ROOT_DP_MENU);

        this.dimMask = new Graphics(this.mainFlow);
        this.mainFlow.getProperties(this.dimMask).set_isAbsolute(true);

        this.cardFlow = new Flow(this.mainFlow);
        this.cardFlow.set_isVertical(false);
        this.cardFlow.set_horizontalAlign(new FlowAlign.Middle());
        this.cardFlow.set_verticalAlign(new FlowAlign.Middle());
        this.cardFlow.set_horizontalSpacing(this.Pixel(20));

        this.mainFlow.getProperties(this.cardFlow).set_isAbsolute(true);

        this.ApplyFullscreenLayout();
        this.ApplyCardFlowLayout();

        this.mainFlow.reflow();
        this.cardFlow.reflow();
    }


    public override void onResize()
    {
        base.onResize();

        if (this.mainFlow == null)
        {
            return;
        }

        this.ApplyFullscreenLayout();
        this.ApplyCardFlowLayout();

        this.mainFlow.reflow();
        this.cardFlow?.reflow();
    }

    public int Pixel(double n) => (int)(this.get_pixelScale.Invoke() * n);

    //构建一张可点击卡片
    public Flow BuildCard(Flow parent, string title, string desc, Action onClick)
    {
        var card = FlowBox.Class.createBoxValidation(parent, default, default, Ref<bool>.In(true), null);
        card.set_isVertical(true);
        card.set_horizontalAlign(new FlowAlign.Middle());
        card.set_verticalAlign(new FlowAlign.Middle());

        var titleText = Assets.Class.makeMedievalText(title.AsHaxeString(), null, card, null);

        dc.ui.Text? descText = null;
        if (!string.IsNullOrEmpty(desc))
        {
            descText = Assets.Class.makeText(desc.AsHaxeString(), 0xAAAAAA, null, card);
        }

        void Resize()
        {
            card.set_minWidth(this.ScreenW / 5);
            card.set_minHeight(this.ScreenH / 2);
            card.set_padding(this.Pixel(20));

            titleText.scaleX = titleText.scaleY = this.get_pixelScale.Invoke() * 0.6;
            titleText.posChanged = true;

            if (descText != null)
            {
                descText.scaleX = descText.scaleY = this.get_pixelScale.Invoke() * 0.3;
                descText.posChanged = true;
            }

            card.reflow();

            if (card.interactive != null)
            {
                card.interactive.width = card.calculatedWidth;
                card.interactive.height = card.calculatedHeight;
            }
        }

        Resize();
        this.resizers[card] = Resize;

        // 交互
        card.set_enableInteractive(true);
        if (card.interactive != null)
        {
            card.interactive.cursor = new Cursor.Button();
            card.interactive.onClick = new HlAction<Event>(_ =>
            {
                AudioHelper.LoadAudioFormString("sfx/ui/menu_select.wav");
                onClick();
            });
            card.interactive.onOver = new(_ => AudioHelper.LoadAudioFormString("sfx/ui/menu_click1.wav"));
        }

        return card;
    }


    //构建一个文本按钮。
    public Flow BuildTextButton(
        Flow parent,
        string label,
        Action onClick,
        int? color = null,
        double scale = 0.4,
        string sound = "sfx/ui/menu_select.wav")
    {
        var btn = new Flow(parent);
        var text = Assets.Class.makeMedievalText(
            label.AsHaxeString(),
            null,
            btn,
            null);

        void Resize()
        {
            btn.set_padding(this.Pixel(6));
            text.scaleX = text.scaleY = this.get_pixelScale.Invoke() * scale;
            text.posChanged = true;
            btn.reflow();

            if (btn.interactive != null)
            {
                btn.interactive.width = btn.calculatedWidth;
                btn.interactive.height = btn.calculatedHeight;
            }
        }

        Resize();
        this.resizers[btn] = Resize;

        btn.set_enableInteractive(true);
        if (btn.interactive != null)
        {
            btn.interactive.cursor = new Cursor.Button();
            btn.interactive.onClick = new HlAction<Event>(_ =>
            {
                AudioHelper.LoadAudioFormString("sfx/ui/menu_select.wav");
                onClick();
            });
            btn.interactive.onOver = new(_ => AudioHelper.LoadAudioFormString("sfx/ui/menu_click1.wav"));
        }

        return btn;
    }

    public Flow BuildBackButton(Flow parent)
        => this.BuildTextButton(parent, "返回", this.OnBackToMain, sound: "sfx/ui/menu_back.wav");


    //按钮规格
    public readonly record struct ButtonSpec(
        string Label,
        Action OnClick,
        int? Color = null,
        double Scale = 0.4,
        string OnClickSound = "sfx/ui/menu_select.wav",
        string OnOverSound = "sfx/ui/menu_click1.wav");


    /// 构建一排横向按钮。
    public Flow BuildButtonRow(Flow parent, params ButtonSpec[] specs)
    {
        var row = new Flow(parent);
        row.set_isVertical(false);
        row.set_horizontalAlign(new FlowAlign.Middle());
        row.set_verticalAlign(new FlowAlign.Middle());
        row.set_minWidth(this.CardW);
        row.set_maxWidth(this.CardW);

        var buttons = new List<(Flow btn, dc.ui.Text text, double scale)>();

        foreach (var spec in specs)
        {
            var btn = new Flow(row);
            var text = Assets.Class.makeMedievalText(
                spec.Label.AsHaxeString(),
                null,
                btn,
                null);

            btn.set_enableInteractive(true);
            if (btn.interactive != null)
            {
                btn.interactive.cursor = new Cursor.Button();
                var click = spec.OnClick;
                var onClickSound = spec.OnClickSound;
                var onOverSound = spec.OnOverSound;
                btn.interactive.onClick = new HlAction<Event>(_ =>
                {
                    AudioHelper.LoadAudioFormString(onClickSound);
                    click();
                });
                btn.interactive.onOver = new(_ => AudioHelper.LoadAudioFormString(onOverSound));

            }

            buttons.Add((btn, text, spec.Scale));
        }

        void Resize()
        {
            row.set_horizontalSpacing(this.Pixel(15));
            row.set_minWidth(this.CardW);
            row.set_maxWidth(this.CardW);

            foreach (var (btn, text, scale) in buttons)
            {
                btn.set_padding(this.Pixel(8));
                text.scaleX = text.scaleY = this.get_pixelScale.Invoke() * scale;
                text.posChanged = true;
                btn.reflow();

                if (btn.interactive != null)
                {
                    btn.interactive.width = btn.calculatedWidth;
                    btn.interactive.height = btn.calculatedHeight;
                }
            }

            row.reflow();
        }

        Resize();
        this.resizers[row] = Resize;

        return row;
    }


    //连接中页面
    private void BuildConnectingPage(Flow parent)
    {
        var text = Assets.Class.makeMedievalText("连接中...".AsHaxeString(), null, parent, null);
        text.scaleX = text.scaleY = this.get_pixelScale.Invoke() * 0.6;
        parent.reflow();
    }


    //屏幕中下方显示提示
    public void ShowToast(string message)
    {
        var toast = Assets.Class.makeMedievalText(message.AsHaxeString(), null, this.mainFlow, null);
        toast.scaleX = toast.scaleY = this.get_pixelScale.Invoke() * 0.4;

        //底部
        this.mainFlow?.getProperties(toast).set_isAbsolute(true);
        toast.x = (this.ScreenW / 2) - (toast.get_textWidth() / 2);
        toast.y = this.ScreenH * 0.85;
        toast.posChanged = true;

        // 淡出
        this.delayer.addF(null, toast.remove, 90);
    }

    //清理
    private void ClearAllPages()
    {
        this.mainMenuView?.Clear();
        this.createRoomView?.Clear();
        this.joinRoomView?.Clear();
        this.roomView?.Clear();
        this.errorView?.Clear();
        this.resizers.Clear();
        this.debugView?.Clear();
    }

    protected override void MyDispose()
    {
        this.mainMenuView?.Dispose();
        this.createRoomView?.Dispose();
        this.joinRoomView?.Dispose();
        this.roomView?.Dispose();
        this.errorView?.Dispose();
        this.debugView?.Dispose();
        this.Hide();
    }
}
