namespace DeadCellsMultiplayerX.Client.UI.Lobby.Views;

using dc.h2d;
using dc.ui;
using DeadCellsMultiplayerX.Client.UI.Common;
using ModCore.Utilities;

internal sealed class JoinRoomView(LobbyView host, Process? parent = null) : UIProcess(parent)
{
    private readonly LobbyView host = host;
    private readonly List<Flow> cards = [];

    public void Build(Flow parent)
    {
        parent.set_isVertical(true);
        parent.set_horizontalAlign(new FlowAlign.Middle());
        parent.set_verticalAlign(new FlowAlign.Middle());

        // 输入卡片
        var inputCard = this.host.BuildCard(parent, "输入 IP 加入", "点击输入服务器地址", this.OnInputClick);
        this.cards.Add(inputCard);

        // 返回按钮
        var back = this.host.BuildBackButton(parent);
        this.cards.Add(back);

        parent.reflow();
    }

    private void OnInputClick()
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

                this.host.OnSubmitJoinRoom(ip, port);
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
