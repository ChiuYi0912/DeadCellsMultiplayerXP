namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using CoreLibrary.Utilities;
using dc;
using dc.h2d;
using dc.hxd;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using HaxeProxy.Runtime;
using ModCore.Utilities;

/// 测试页面
internal sealed class DebugView : UIProcess
{
    private readonly LobbyView host;
    private Flow? contentFlow;
    private readonly List<Flow> items = [];
    private readonly Dictionary<Flow, Action> resizers = [];

    public DebugView(LobbyView host, Process? parent = null) : base(parent)
    {
        this.host = host;
        OnScreenResized += this.MyOnResize;
    }

    public void Build(Flow parent)
    {
        var content = new Flow(parent);
        content.set_isVertical(true);
        content.set_horizontalAlign(new FlowAlign.Middle());
        content.set_verticalAlign(new FlowAlign.Middle());
        content.set_verticalSpacing(this.host.Pixel(12));
        content.set_minWidth(this.host.CardW);
        content.set_maxWidth(this.host.CardW);

        this.contentFlow = content;

        this.AddTitle(content, "Debug Menu");

        this.AddDivider(content);

        this.AddDebugToggle(content, "显示网络统计", () => ToggleFlag("netstat"));
        this.AddDebugToggle(content, "显示实体调试标签", () => ToggleFlag("entityLabels"));
#if DEBUG
        // DebugLauncher is compiled only under DEBUG (see its file-level #if).
        this.AddDebugBtn(content, "本地2人测试", () => DebugLauncher.StartTest(1));
        this.AddDebugBtn(content, "本地3人测试", () => DebugLauncher.StartTest(2));
        this.AddDebugBtn(content, "本地4人测试", () => DebugLauncher.StartTest(3));
#endif

        this.AddDivider(content);

        var back = this.host.BuildBackButton(content);
        this.items.Add(back);

        content.reflow();
        parent.reflow();
    }


    private void AddTitle(Flow parent, string text)
    {
        var flow = new Flow(parent);
        var t = Assets.Class.makeMedievalText(text.AsHaxeString(), null, flow, null);
        t.scaleX = t.scaleY = this.host.get_pixelScale.Invoke() * 0.5;

        void Resize()
        {
            t.scaleX = t.scaleY = this.host.get_pixelScale.Invoke() * 0.5;
            t.posChanged = true;
        }
        Resize();
        this.resizers[flow] = Resize;
        this.items.Add(flow);
    }

    private void AddDivider(Flow parent)
    {
        var g = new Graphics(parent);
        g.beginFill(Ref<int>.In(0xFFFFFF), Ref<double>.In(0.15));
        g.drawRect(0, 0, this.host.CardW - this.host.Pixel(40), 1);
        g.endFill();
    }

    private void AddDebugBtn(Flow parent, string label, Action cb)
    {
        var btn = new Flow(parent);
        var text = Assets.Class.makeMedievalText(label.AsHaxeString(), null, btn, null);

        void Resize()
        {
            btn.set_padding(this.host.Pixel(8));
            text.scaleX = text.scaleY = this.host.get_pixelScale.Invoke() * 0.4;
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
                cb();
            });
        }
        this.items.Add(btn);
    }

    private void AddDebugToggle(Flow parent, string label, Action cb) => this.AddDebugBtn(parent, $"[切换] {label}", cb);


    private static void ToggleFlag(string key)
    {
        DebugFlags.Toggle(key);
        ModEntry.Instance.Logger.Information($"[Debug] {key} = {DebugFlags.Get(key)}");
    }

    public void Refresh(LobbyContext ctx) { }

    private void MyOnResize()
    {
        if (this.contentFlow == null)
        {
            return;
        }

        this.contentFlow.set_minWidth(this.host.CardW);
        this.contentFlow.set_maxWidth(this.host.CardW);

        foreach (var r in this.resizers.Values)
        {
            try
            { r(); }
            catch (Exception ex) { this.Logger.Warning(ex, "Debug resize failed"); }
        }

        this.contentFlow.reflow();
    }

    public void Clear()
    {
        foreach (var item in this.items)
        {
            this.resizers.Remove(item);
            item.remove();
        }
        this.items.Clear();
        this.resizers.Clear();

        this.contentFlow?.removeChildren();
        this.contentFlow?.remove();
        this.contentFlow = null;
    }

    protected override void MyDispose()
    {
        OnScreenResized -= this.MyOnResize;
        this.Clear();
    }
}
