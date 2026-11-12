namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using ModCore.Utilities;

internal sealed class CreateRoomView(LobbyView host, Process? parent = null) : UIProcess(parent)
{
    private readonly LobbyView host = host;
    private readonly List<Flow> cards = [];

    public void Build(Flow parent)
    {
        parent.set_isVertical(true);
        parent.set_horizontalAlign(new FlowAlign.Middle());
        parent.set_verticalAlign(new FlowAlign.Middle());

        // 卡片行
        var cardRow = new Flow(parent);
        cardRow.set_isVertical(false);
        cardRow.set_horizontalAlign(new FlowAlign.Middle());
        cardRow.set_horizontalSpacing(this.host.Pixel(30));

        // IP联机
        var ipCard = this.host.BuildCard(cardRow, "IP 直连", "通过 IP 连接", this.ShowIPInput);
        this.cards.Add(ipCard);

        // Steam联机
        var steamCard = this.host.BuildCard(cardRow, "Steam P2P", "暂不可用", this.OnSelectSteam);
        this.cards.Add(steamCard);

        cardRow.reflow();

        var back = this.host.BuildBackButton(parent);
        this.cards.Add(back);

        parent.reflow();
    }

    private void OnSelectSteam() => ModEntry.Instance.Logger.Information("Steam 模式暂未支持");

    private void ShowIPInput()
    {
        var input = new dc.ui.TextInput(
            this.host,
            "输入 IP 和端口".AsHaxeString(),
            "127.0.0.1:12345".AsHaxeString(),
            "127.0.0.1:12345".AsHaxeString(),
            str =>
            {
                if (!TryParseIpPort(str.ToString(), out var ip, out var port))
                {
                    return;
                }

                this.host.OnSubmitCreateRoom(ip, port);
            },
            "回车确认".AsHaxeString(),
            null,
            null);
    }

    private static bool TryParseIpPort(string input, out string ip, out int port)
    {
        ip = string.Empty;
        port = 0;
        var parts = input.Split([':', ' '], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        if (!int.TryParse(parts[^1], out port))
        {
            return false;
        }

        ip = parts[0];
        return true;
    }

    public void Refresh(LobbyContext ctx) { }

    public void Clear()
    {
        foreach (var c in this.cards)
        {
            c.remove();
        }

        this.cards.Clear();
    }

    protected override void MyDispose() => this.Clear();
}
