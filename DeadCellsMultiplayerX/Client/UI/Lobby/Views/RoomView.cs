namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using ModCore.Utilities;
using static DeadCellsMultiplayerX.Client.UI.Lobby.Views.LobbyView;

internal sealed class RoomView : UIProcess
{
    private readonly LobbyView host;
    private PlayerSlotPanel? panel;
    private Flow? btnRow;
    private Flow? contentFlow;

    public RoomView(LobbyView host, Process? parent = null) : base(parent)
    {
        this.host = host;
        OnScreenResized += this.MyonResize;
    }

    public void Build(Flow parent)
    {
        var content = new Flow(parent);
        content.set_isVertical(true);
        content.set_horizontalAlign(new FlowAlign.Middle());
        content.set_verticalAlign(new FlowAlign.Middle());
        content.set_verticalSpacing(this.host.Pixel(20));
        content.set_minWidth(this.host.CardW);
        content.set_maxWidth(this.host.CardW);

        this.contentFlow = content;

        this.panel = new PlayerSlotPanel(this.host, content, this.host);

        this.btnRow = new Flow(content);
        this.btnRow.set_isVertical(false);
        this.btnRow.set_horizontalAlign(new FlowAlign.Middle());
        this.btnRow.set_horizontalSpacing(this.host.Pixel(15));
        this.btnRow.set_minWidth(this.host.CardW);
        this.btnRow.set_maxWidth(this.host.CardW);

        this.btnRow = this.host.BuildButtonRow(content, [.. this.MakeRoomButtons()]);

        content.reflow();
        parent.reflow();
    }

    private IEnumerable<ButtonSpec> MakeRoomButtons()
    {
        var ctx = this.host.Context;
        if (ctx.IsHost)
        {
            yield return new ButtonSpec("开始游戏", this.host.OnClickStartGame);
            yield return new ButtonSpec("销毁房间", this.host.OnClickLeaveRoom);
        }
        else
        {
            yield return new ButtonSpec("准备 / 取消", this.host.OnClickToggleReady);
            yield return new ButtonSpec("离开房间", this.host.OnClickLeaveRoom);
        }
    }


    public void MyonResize()
    {
        if (this.contentFlow == null)
        {
            return;
        }

        this.contentFlow.set_minWidth(this.host.CardW);
        this.contentFlow.set_maxWidth(this.host.CardW);


        if (this.btnRow != null)
        {
            this.btnRow.set_minWidth(this.host.CardW);
            this.btnRow.set_maxWidth(this.host.CardW);
            this.btnRow.reflow();
        }

        this.contentFlow.reflow();
    }

    public void Refresh(LobbyContext ctx)
    {
        this.panel?.Refresh(ctx.Lobby?.Guests.Values);

        if (this.panel != null && this.panel.TitleText != null)
        {
            var color = ctx.LatencyMs <= 50 ? "00FF00"
                      : ctx.LatencyMs <= 100 ? "FFFF00"
                      : "FF0000";
            var text = $"人数: {ctx.Lobby?.Guests.Count ?? 0}  延迟: <font color=\"#{color}\">{ctx.LatencyMs}ms</font>";
            this.panel.TitleText.set_text(text.AsHaxeString());
        }
    }

    public void Clear()
    {

        this.panel?.Dispose();
        this.panel = null;

        this.contentFlow?.removeChildren();
        this.contentFlow?.remove();
        this.contentFlow = null;
    }

    protected override void MyDispose()
    {
        OnScreenResized -= this.MyonResize;
        this.Clear();
    }
}
