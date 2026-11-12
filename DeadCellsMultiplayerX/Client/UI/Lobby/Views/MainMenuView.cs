namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using static DeadCellsMultiplayerX.Client.UI.Lobby.Views.LobbyView;

internal sealed class MainMenuView(LobbyView host, Process? parent = null) : UIProcess(parent)
{

    private readonly LobbyView host = host;
    private readonly List<Flow> cards = [];
    private Flow? footerRow;

    public void Build(Flow parent)
    {
        parent.set_isVertical(true);
        parent.set_horizontalAlign(new FlowAlign.Middle());
        parent.set_verticalAlign(new FlowAlign.Middle());
        parent.set_verticalSpacing(this.host.Pixel(30));

        // 卡片行
        var cardRow = new Flow(parent);
        cardRow.set_isVertical(false);
        cardRow.set_horizontalAlign(new FlowAlign.Middle());
        cardRow.set_verticalAlign(new FlowAlign.Middle());
        cardRow.set_horizontalSpacing(this.host.Pixel(30));

        var createCard = this.host.BuildCard(cardRow, "创建房间", "建立新大厅",
            this.host.OnGoCreateRoom);
        this.cards.Add(createCard);

        var joinCard = this.host.BuildCard(cardRow, "加入房间", "输入 IP 加入",
            this.host.OnGoJoinRoom);
        this.cards.Add(joinCard);

        cardRow.reflow();

        this.footerRow = this.host.BuildButtonRow(parent, [.. this.MakeFooterButtons()]);

        parent.reflow();
    }

    private IEnumerable<ButtonSpec> MakeFooterButtons()
    {
        yield return new ButtonSpec(
            Label: "返回主页",
            OnClick: this.host.OnRequestClose,
            OnClickSound: "sfx/ui/menu_back.wav");

#if DEBUG
        yield return new ButtonSpec(
            Label: "[DEBUG]",
            OnClick: this.host.GotoDebug,
            Color: 0xFF00FF,
            Scale: 0.35);
#endif
    }

    public void Refresh(LobbyContext ctx) { }

    public void Clear()
    {
        foreach (var card in this.cards)
        {
            card.remove();
        }

        this.cards.Clear();
    }

    protected override void MyDispose() => this.Clear();
}
