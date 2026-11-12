namespace DeadCellsMultiplayerX.Client.UI.Common;

using dc;
using dc.ui;
using Microsoft;
using ModCore.Events;
using Serilog;

/// <summary>
/// UI的基类
/// </summary>
public abstract class UIProcess : Process, IEventReceiver, IDisposableObservable
{
    private readonly CancellationTokenSource disposedSource = new();
    public CancellationToken DisposeToken => this.disposedSource.Token;
    public bool IsDisposed => this.disposedSource.IsCancellationRequested;
    public virtual ILogger Logger { get; }

    public event Action? OnScreenResized;

    public int ScreenW
    {
        get
        {
            var w = dc.libs.Process.Class.CUSTOM_STAGE_WIDTH > 0
            ? dc.libs.Process.Class.CUSTOM_STAGE_WIDTH
            : dc.hxd.Window.Class.getInstance().get_width();
            this.ScreenW = w;
            return field;
        }
        private set;
    } = -1;

    public int ScreenH
    {
        get
        {
            var h = dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT > 0
            ? dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT
            : dc.hxd.Window.Class.getInstance().get_height();
            this.ScreenH = h;
            return field;
        }
        private set;
    } = -1;

    protected UIProcess(Process? parent, int? dpLayer = null) : base(parent)
    {
        this.Logger = Log.ForContext(this.GetType());
        EventSystem.AddReceiver(this);

        var layers = Main.Class.ME?.root;
        if (layers != null)
        {
            var layer = dpLayer ?? Const.Class.ROOT_DP_MENU;
            this.createRootInLayers(layers, layer);
        }

        this.CalcScreenSize();
    }

    public override void onResize()
    {
        base.onResize();
        this.CalcScreenSize();
        OnScreenResized?.Invoke();
    }

    protected void CalcScreenSize()
    {
        var w = dc.libs.Process.Class.CUSTOM_STAGE_WIDTH > 0
            ? dc.libs.Process.Class.CUSTOM_STAGE_WIDTH
            : dc.hxd.Window.Class.getInstance().get_width();

        var h = dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT > 0
            ? dc.libs.Process.Class.CUSTOM_STAGE_HEIGHT
            : dc.hxd.Window.Class.getInstance().get_height();

        if (w == this.ScreenW && h == this.ScreenH)
        {
            return;
        }

        this.ScreenW = w;
        this.ScreenH = h;
    }

    protected abstract void MyDispose();

    public void Dispose()
    {
        if (this.IsDisposed)
        {
            return;
        }

        GC.SuppressFinalize(this);
        this.onDispose();
    }

    public override void onDispose()
    {
        if (!this.IsDisposed)
        {
            this.disposedSource.Cancel();
        }

        EventSystem.RemoveReceiver(this);

        this.MyDispose();

        this.destroy();
    }
}
