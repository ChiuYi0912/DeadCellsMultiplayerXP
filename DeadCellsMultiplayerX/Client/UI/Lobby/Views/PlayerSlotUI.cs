namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc;
using dc.h2d;
using dc.libs.heaps.slib;
using dc.tool;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using DeadCellsMultiplayerX.Contracts.Lobby;
using ModCore.Utilities;
using Serilog;

internal sealed class PlayerSlotUI
{
    public Flow Container { get; }
    public HSprite? HeroSprite { get; private set; }
    public dc.ui.Text? NameLabel { get; private set; }
    public dc.ui.Text? StatusLabel { get; private set; }

    public GuestInfo? Guest { get; set; }
    public bool IsOccupied => this.Guest != null;

    private readonly LobbyView host;
    private readonly ILogger logger;
    private Flow? emptyBox;
    private GuestInfo? prevSnapshot;

    public event Action<PlayerSlotUI>? OnChanged;

    public PlayerSlotUI(Flow parent, LobbyView host)
    {
        this.host = host;
        this.logger = Log.ForContext<PlayerSlotUI>();

        this.Container = new Flow(parent);
        this.Container.set_isVertical(true);
        this.Container.set_verticalAlign(new FlowAlign.Middle());
        this.Container.set_horizontalAlign(new FlowAlign.Middle());

        this.ShowEmpty();
    }

    public void Bind(GuestInfo guest, bool force = false)
    {
        if (string.IsNullOrEmpty(guest.SkinMould))
        {
            return;
        }

        //已绑定同一玩家
        if (!force && this.Guest?.Guid == guest.Guid)
        {
            this.Guest = guest;
            this.NameLabel?.set_text(guest.Name.AsHaxeString());

            this.RefreshStatus();
            return;
        }

        //新玩家
        this.Guest = guest;
        this.ClearContent();
        this.HideEmpty();

        var skin = guest.IsHost
            ? Save.Class.tryLoad().heroSkin.ToString()
            : guest.SkinMould;

        this.HeroSprite = UIRenderer.CreateHeroSpr(skin, this.Container);
        this.NameLabel = Assets.Class.makeText(guest.Name.AsHaxeString(), 0xDDDDDD, null, this.Container);
        this.StatusLabel = Assets.Class.makeText("".AsHaxeString(), null, null, this.Container);

        this.RefreshStatus();
        this.ApplyScale();

        OnChanged?.Invoke(this);
    }

    public void Clear()
    {
        if (this.Guest == null)
        {
            return;
        }

        this.Guest = null;
        this.prevSnapshot = null;

        this.ClearContent();
        this.ShowEmpty();

        this.Container.reflow();
        OnChanged?.Invoke(this);
    }

    private void ClearContent()
    {
        this.HeroSprite?.remove();
        this.HeroSprite = null;
        this.NameLabel?.remove();
        this.NameLabel = null;
        this.StatusLabel?.remove();
        this.StatusLabel = null;
    }

    //空位
    private void ShowEmpty()
    {
        if (this.emptyBox != null)
        {
            return;
        }

        this.emptyBox = new Flow(this.Container);
        var text = Assets.Class.makeText("[Empty]".AsHaxeString(), 0x666666, null, this.emptyBox);
        this.ApplyEmptyScale(text);
    }

    private void HideEmpty()
    {
        if (this.emptyBox == null)
        {
            return;
        }

        this.emptyBox.removeChildren();
        this.emptyBox.remove();
        this.emptyBox = null;
    }

    //状态刷新
    public void RefreshStatus()
    {
        if (this.Guest == null || this.StatusLabel == null)
        {
            return;
        }

        var status = this.Guest.IsHost ? "[Host]"
                      : this.Guest.IsReady ? "[Ready]"
                      : "[...]";

        var color = this.Guest.IsHost ? 0xFFDD44
                  : this.Guest.IsReady ? 0x44FF44
                  : 0x888888;

        this.StatusLabel.set_text(status.AsHaxeString());
        this.StatusLabel.set_textColor(color);
        this.StatusLabel.posChanged = true;

        //名称皮肤变化重绑
        if (this.prevSnapshot == null)
        {
            this.prevSnapshot = this.Guest.Clone();
            return;
        }

        if (this.prevSnapshot.SkinMould != this.Guest.SkinMould
            || this.prevSnapshot.Name != this.Guest.Name
            || this.prevSnapshot.IsHost != this.Guest.IsHost)
        {
            this.prevSnapshot = this.Guest.Clone();
            this.Bind(this.Guest, force: true);
        }
    }

    //缩放
    public void ApplyScale()
    {
        var scale = this.host.get_pixelScale.Invoke();

        if (this.emptyBox != null)
        {
            foreach (var child in this.emptyBox.children)
            {
                if (child is dc.ui.Text t)
                {
                    this.ApplyEmptyScale(t, scale);
                }
            }
        }

        if (this.HeroSprite != null)
        {
            this.HeroSprite.scaleX = this.HeroSprite.scaleY = scale * 0.5;
            this.HeroSprite.posChanged = true;
        }
        if (this.NameLabel != null)
        {
            this.NameLabel.scaleX = this.NameLabel.scaleY = scale * 0.30;
            this.NameLabel.posChanged = true;
        }
        if (this.StatusLabel != null)
        {
            this.StatusLabel.scaleX = this.StatusLabel.scaleY = scale * 0.25;
            this.StatusLabel.posChanged = true;
        }
    }

    private void ApplyEmptyScale(dc.ui.Text text, double? scale = null)
    {
        scale ??= this.host.get_pixelScale.Invoke();
        text.scaleX = text.scaleY = scale.Value * 0.25;
        text.posChanged = true;
    }
}


internal sealed class PlayerSlotPanel : UIProcess
{
    private const int MAX_SLOTS = 4;

    public Flow Root { get; private set; }
    public Flow Container { get; private set; }
    public Flow Title { get; private set; }
    public HtmlText TitleText { get; }
    public PlayerSlotUI[] Slots { get; }

    private readonly LobbyView host;
    private readonly Queue<string> nameQueue = new();
    private bool isShowingName;

    public PlayerSlotPanel(LobbyView host, Flow parent, Process? processParent = null)
        : base(processParent)
    {
        this.host = host;

        this.Root = new Flow(parent);
        this.Root.set_isVertical(true);
        this.Root.set_horizontalAlign(new FlowAlign.Middle());
        this.Root.set_verticalAlign(new FlowAlign.Middle());
        this.Root.set_verticalSpacing(host.Pixel(10));
        this.Root.set_minWidth(host.CardW);
        this.Root.set_maxWidth(host.CardW);

        //标题
        this.Title = new Flow(this.Root);
        this.Title.set_isVertical(false);
        this.Title.set_horizontalAlign(new FlowAlign.Middle());
        this.Title.set_verticalAlign(new FlowAlign.Top());

        this.TitleText = new HtmlText(Assets.Class.font12, this.Title);
        this.ApplyTitleFont();

        //卡槽容器
        this.Container = new Flow(this.Root);
        this.Container.set_isVertical(false);
        this.Container.set_horizontalAlign(new FlowAlign.Middle());
        this.Container.set_verticalAlign(new FlowAlign.Middle());
        this.Container.set_horizontalSpacing(host.Pixel(20));

        //创建4个卡槽
        this.Slots = new PlayerSlotUI[MAX_SLOTS];
        for (var i = 0; i < MAX_SLOTS; i++)
        {
            this.Slots[i] = new PlayerSlotUI(this.Container, host);
            this.Slots[i].OnChanged += this.OnSlotChanged;
        }

        this.ApplyLayout();

        //订阅屏幕变化
        host.OnScreenResized += this.ApplyLayout;
    }


    public void Refresh(IEnumerable<GuestInfo>? guests)
    {
        var list = guests?.Take(MAX_SLOTS).ToList() ?? [];

        //移除已离开的
        for (var i = 0; i < MAX_SLOTS; i++)
        {
            var g = this.Slots[i].Guest;
            if (g != null && !list.Any(x => x.Guid == g.Guid))
            {
                this.Slots[i].Clear();
            }
        }

        //绑定和刷新
        for (var i = 0; i < list.Count; i++)
        {
            var guest = list[i];
            if (this.Slots[i].Guest?.Guid == guest.Guid)
            {
                this.Slots[i].Guest = guest;
                this.Slots[i].RefreshStatus();
            }
            else
            {
                this.Slots[i].Clear();
                this.Slots[i].Bind(guest);
            }
        }

        //多余槽清空
        for (var i = list.Count; i < MAX_SLOTS; i++)
        {
            this.Slots[i].Clear();
        }
    }

    private void ApplyLayout()
    {
        this.Root.set_minWidth(this.host.CardW);
        this.Root.set_maxWidth(this.host.CardW);

        this.Title.set_minWidth(this.host.CardW);
        this.Title.set_minHeight(this.host.CardH / 8);
        this.Title.set_maxHeight(this.host.CardH / 8);

        this.Container.set_minWidth(this.host.CardW);

        var spacing = this.host.Pixel(20);
        this.Container.set_horizontalSpacing(spacing);
        this.Container.set_minWidth(this.host.CardW);
        this.Container.set_maxWidth(this.host.CardW);

        var totalSpacing = spacing * (MAX_SLOTS - 1);
        var slotW = (this.host.CardW - totalSpacing) / MAX_SLOTS;

        foreach (var s in this.Slots)
        {
            s.Container.set_minWidth(slotW);
            s.Container.set_maxWidth(slotW);
            s.ApplyScale();
            s.Container.reflow();
        }

        this.ApplyTitleFont();

        this.Title.reflow();
        this.Container.reflow();
        this.Root.reflow();
    }

    public void ClearAll()
    {
        foreach (var s in this.Slots)
        {
            s.Clear();
        }

        this.Title.removeChildren();
        this.Title.remove();
        this.Container.removeChildren();
        this.Container.remove();
        this.Root.removeChildren();
        this.Root.remove();
    }

    private void ApplyTitleFont()
    {
        var pixelScale = this.host.get_pixelScale.Invoke();
        var font = pixelScale <= 3.0 ? Assets.Class.fontConf.font12
                 : pixelScale <= 4.0 ? Assets.Class.fontConf.font18
                 : pixelScale <= 6.0 ? Assets.Class.fontConf.font12
                 : Assets.Class.fontConf.font18;

        this.TitleText.set_font(font.toFont());
        this.TitleText.scaleX = this.TitleText.scaleY = pixelScale * 0.25;
        this.TitleText.posChanged = true;
    }

    //新玩家提示
    private void OnSlotChanged(PlayerSlotUI slot)
    {
        var name = slot.Guest?.Name;
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        this.nameQueue.Enqueue(name);
        if (!this.isShowingName)
        {
            this.ShowNextName();
        }
    }

    private void ShowNextName()
    {
        if (this.nameQueue.Count == 0)
        {
            this.isShowingName = false;
            return;
        }

        this.isShowingName = true;
        var next = this.nameQueue.Dequeue();
        this.host.ShowToast($"玩家 {next} 加入");
        this.ShowNextName();
    }

    protected override void MyDispose()
    {
        this.host.OnScreenResized -= this.ApplyLayout;

        foreach (var s in this.Slots)
        {
            s.OnChanged -= this.OnSlotChanged;
        }

        this.ClearAll();
    }
}
