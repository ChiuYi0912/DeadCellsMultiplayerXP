using DeadCellsMultiplayerX.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Quic;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Networks.Quic
{
    internal class QuicNetworkConnect : BaseNetworkConnection
    {
        private QuicConnection connection;
        private QuicStream? stream;
        private bool isClient;
        public QuicNetworkConnect(QuicConnection connection, bool isClient = true)
        {
            this.connection = connection;
            this.isClient = isClient;
        }

        public override async Task Init(CancellationToken cancellationToken)
        {
            if (isClient)
            {
                stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken);
            }
            else
            {
                stream = await connection.AcceptInboundStreamAsync(cancellationToken);
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
            _  = connection?.DisposeAsync();
        }
    }
}
