using System.Net;
using System.Net.Quic;
namespace DeadCellsMultiplayerX.Networking.Transport.Quic
{
    internal class QuicNetworkConnect : BaseNetworkConnection
    {
        private QuicConnection? connection;
        private QuicStream? stream;
        private bool isClient;
        public QuicNetworkConnect(QuicConnection connection, bool isClient = true)
        {
            this.connection = connection;
            this.isClient = isClient;
        }

        public override async Task Init(CancellationToken cancellationToken)
        {
            var current = connection
                ?? throw new ObjectDisposedException(nameof(QuicNetworkConnect));

            if (isClient)
            {
                stream = await current.OpenOutboundStreamAsync(
                    QuicStreamType.Bidirectional, cancellationToken);
            }
            else
            {
                stream = await current.AcceptInboundStreamAsync(cancellationToken);
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
                    {
                        // 服务器证书运行时生成
                        return true; //忽略所有证书
                    }
                },
                DefaultStreamErrorCode = 0x0A,
                DefaultCloseErrorCode = 0x0B
            }, cancellationToken);

            var nc = new QuicNetworkConnect(connect, true);
            await nc.Init(cancellationToken);
            return nc;
        }

        public override Stream Stream => stream ?? throw new NullReferenceException();

        protected override void MyDispose()
        {
            var current = Interlocked.Exchange(ref connection, null);
            stream = null;
            if (current == null)
                return;

            try
            {
                // Sync Dispose is the fallback for callers that cannot await.
                // Owners that support async disposal call DisposeAsync instead.
                DisposeAsyncBlocking(() => current.DisposeAsync());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "QUIC connection dispose failed");
            }
        }

        protected override async ValueTask MyDisposeAsync()
        {
            var current = Interlocked.Exchange(ref connection, null);
            stream = null;
            if (current != null)
                await current.DisposeAsync().ConfigureAwait(false);
        }
    }
}
