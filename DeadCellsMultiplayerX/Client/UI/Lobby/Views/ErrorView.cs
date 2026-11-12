namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc;
using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using ModCore.Utilities;

internal sealed class ErrorView(LobbyView host, Process? parent = null) : UIProcess(parent)
{
    private readonly LobbyView host = host;
    private dc.ui.Text? errorText;
    private readonly List<Flow> cards = [];

    public void Build(Flow parent, string? message)
    {
        parent.set_isVertical(true);
        parent.set_horizontalAlign(new FlowAlign.Middle());
        parent.set_verticalAlign(new FlowAlign.Middle());

        this.errorText = Assets.Class.makeText(
            (message ?? "未知错误").AsHaxeString(), 0xFF4444, null, parent);
        this.errorText.scaleX = this.errorText.scaleY = this.host.get_pixelScale.Invoke() * 0.4;

        var back = this.host.BuildBackButton(parent);
        this.cards.Add(back);

        parent.reflow();
    }

    public void Refresh(LobbyContext ctx)
    {
        if (this.errorText != null && !string.IsNullOrEmpty(ctx.LastError))
        {
            this.errorText.set_text(ctx.LastError.AsHaxeString());
        }
    }

    public void Clear()
    {
        this.errorText?.remove();
        this.errorText = null;
        foreach (var c in this.cards)
        {
            c.remove();
        }

        this.cards.Clear();
    }

    protected override void MyDispose() => this.Clear();
}
