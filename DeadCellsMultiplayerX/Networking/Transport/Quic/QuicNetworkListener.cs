using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using DeadCellsMultiplayerX.Utilities.Threading;
namespace DeadCellsMultiplayerX.Networking.Transport.Quic
{
    internal class QuicNetworkListener(string ip, int port) : BaseNetworkListener
    {
        private static readonly X509Certificate certificate;

        static QuicNetworkListener()
        {
            certificate = CertificateUtils.CreateSelfSignedCertificate("CN=DeadCellsMP");
        }

        private QuicListener? listener;
        public override async Task Init()
        {
            listener = await QuicListener.ListenAsync(new QuicListenerOptions()
            {
                ListenEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse(ip), port),
                ApplicationProtocols = [
                    new("dccm-mp-protocol")
                    ],
                ConnectionOptionsCallback = (connection, sslInfo, cancellationToken) =>
                {
                    var serverOptions = new QuicServerConnectionOptions
                    {
                        DefaultStreamErrorCode = 0x0A,
                        DefaultCloseErrorCode = 0x0B,
                        ServerAuthenticationOptions = new SslServerAuthenticationOptions
                        {
                            ApplicationProtocols = [
                                new("dccm-mp-protocol")
                                ],
                            ServerCertificate = certificate
                        }
                    };
                    return ValueTask.FromResult(serverOptions);
                }
            }, DisposeToken);
        }
        protected override void MyDispose()
        {
            var current = Interlocked.Exchange(ref listener, null);
            if (current == null)
                return;

            try
            {
                // Sync Dispose is the fallback for callers that cannot await.
                DisposeAsyncBlocking(() => current.DisposeAsync());
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "QUIC listener dispose failed");
            }
        }

        protected override async ValueTask MyDisposeAsync()
        {
            var current = Interlocked.Exchange(ref listener, null);
            if (current != null)
                await current.DisposeAsync().ConfigureAwait(false);
        }

        public override async Task<BaseNetworkConnection> WaitConnect(CancellationToken cancellationToken)
        {
            var current = listener
                ?? throw new ObjectDisposedException(nameof(QuicNetworkListener));

            var connect = await current.AcceptConnectionAsync(cancellationToken.Combine(DisposeToken));
            var nc = new QuicNetworkConnect(connect, false);
            await nc.Init(cancellationToken.Combine(DisposeToken));
            return nc;
        }
    }
}
