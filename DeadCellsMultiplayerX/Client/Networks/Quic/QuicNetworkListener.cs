using DeadCellsMultiplayerX.Client.Networks.Quic;
using DeadCellsMultiplayerX.Utils;
using Microsoft.VisualStudio.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Networks.Quic
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
            _  = listener?.DisposeAsync();
        }

        public override async Task<BaseNetworkConnection> WaitConnect(CancellationToken cancellationToken)
        {
            Debug.Assert(listener != null);

            var connect = await listener.AcceptConnectionAsync(cancellationToken.Combine(DisposeToken));
            var nc = new QuicNetworkConnect(connect, false);
            await nc.Init(cancellationToken.Combine(DisposeToken));
            return nc;
        }
    }
}
