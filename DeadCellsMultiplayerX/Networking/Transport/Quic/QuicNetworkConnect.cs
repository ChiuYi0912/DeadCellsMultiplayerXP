namespace DeadCellsMultiplayerX.Networking.Transport.Quic;

using System.Net;
using System.Net.Quic;

internal sealed class QuicNetworkConnect(QuicConnection connection, bool isClient = true) : BaseNetworkConnection
{
    private QuicConnection? connection = connection;
    private QuicStream? stream;
    private readonly bool isClient = isClient;

    public override async Task Init(CancellationToken cancellationToken)
    {
        var current = this.connection
            ?? throw new ObjectDisposedException(nameof(QuicNetworkConnect));

        if (this.isClient)
        {
            this.stream = await current.OpenOutboundStreamAsync(
                QuicStreamType.Bidirectional, cancellationToken);
        }
        else
        {
            this.stream = await current.AcceptInboundStreamAsync(cancellationToken);
        }
    }

    public static async Task<BaseNetworkConnection> Connect(string ip, int port, CancellationToken cancellationToken)
    {
        var connect = await QuicConnection.ConnectAsync(new()
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port),
            ClientAuthenticationOptions = new()
            {
                ApplicationProtocols = [
                    new("dccm-mp-protocol")
                    ],
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                    // 服务器证书运行时生成
                    true
            },
            DefaultStreamErrorCode = 0x0A,
            DefaultCloseErrorCode = 0x0B
        }, cancellationToken);

        var nc = new QuicNetworkConnect(connect, true);
        await nc.Init(cancellationToken);
        return nc;
    }

    public override Stream Stream => this.stream ?? throw new InvalidOperationException(
        "The QUIC stream is not initialized.");

    protected override void MyDispose()
    {
        var current = Interlocked.Exchange(ref this.connection, null);
        this.stream = null;
        if (current == null)
        {
            return;
        }

        try
        {
            // Sync Dispose is the fallback for callers that cannot await.
            // Owners that support async disposal call DisposeAsync instead.
            DisposeAsyncBlocking(current.DisposeAsync);
        }
        catch (Exception ex)
        {
            this.Logger.Warning(ex, "QUIC connection dispose failed");
        }
    }

    protected override async ValueTask MyDisposeAsync()
    {
        var current = Interlocked.Exchange(ref this.connection, null);
        this.stream = null;
        if (current != null)
        {
            await current.DisposeAsync().ConfigureAwait(false);
        }
    }
}
