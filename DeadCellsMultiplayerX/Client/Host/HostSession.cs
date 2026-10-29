using DeadCellsMultiplayerX.Common.Core;
using DeadCellsMultiplayerX.Server.Core;
using Microsoft;
using Microsoft.VisualStudio.Threading;
using ModCore.Utilities;
using Nerdbank.Streams;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using DeadCellsMultiplayerX.Client.Core;

namespace DeadCellsMultiplayerX.Client.Host
{
    internal class HostSession : Disposable, IDisposable
    {
        private AnonymousPipeServerStream? inPipe;
        private AnonymousPipeServerStream? outPipe;

        private MultiplexingStream? multiplexingStream;
        private MultiplexingStream.Channel? mainChannel;
        private BinaryWriter? mainChannelWriter;
        private Process? serverProcess;
        private bool canJoin = true;

        private ILogger serverLogger = Log.Logger.ForContext("SourceContext", "Server");

        public async Task Init()
        {
            await Task.Yield().ConfigureAwait(false); //不在主线程上执行

            Logger.Information("Starting server...");

            inPipe = new(PipeDirection.In, HandleInheritability.Inheritable);
            outPipe = new(PipeDirection.Out, HandleInheritability.Inheritable);

            serverProcess = WorkerProcessUtils.StartWorkerProcess(typeof(ServerMain).AssemblyQualifiedName!, nameof(ServerMain.Entrypoint),
                new()
                {
                    EnvironmentVariables =
                    {
                        ["DCMP_HOST_IN_PIPE"] = inPipe.GetClientHandleAsString(),
                        ["DCMP_HOST_OUT_PIPE"] = outPipe.GetClientHandleAsString(),
                    },
                    RedirectStandardOutput = true,
                    RedirectStandardError = false
                }, typeof(ServerMain).Assembly.Location);

            serverProcess.Exited += ServerProcess_Exited;
            serverProcess.EnableRaisingEvents = true;
            //serverProcess.BeginErrorReadLine();
            serverProcess.BeginOutputReadLine();

            serverProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
            //serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

            inPipe.DisposeLocalCopyOfClientHandle();
            outPipe.DisposeLocalCopyOfClientHandle();

            Logger.Information("Waiting server...");

            while(!inPipe.IsConnected)
            {
                DisposeToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            {
                int v = 0;
                while((v = inPipe.ReadByte()) != 0x32)
                {
                    DisposeToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
            }
           // await inPipe.ReadAtLeastAsync(new byte[1], 1, false); //等待服务器连接

            multiplexingStream = await MultiplexingStream.CreateAsync(
                FullDuplexStream.Splice(inPipe, outPipe),
                new MultiplexingStream.Options()
                {
                    ProtocolMajorVersion = 3
                },
                DisposeToken
                );

            Debug.Assert(!((IDisposableObservable)multiplexingStream).IsDisposed);
            mainChannel = await multiplexingStream.OfferChannelAsync("main");
            mainChannelWriter = new(mainChannel.AsStream());

            Logger.Information("Server started");
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
           
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            serverLogger.Information(e.Data ?? "");
        }

        private void ServerProcess_Exited(object? sender, EventArgs e)
        {
            Logger.Information("Server process exited.");
            Dispose();
        }

        public void StartGame()
        {
            Debug.Assert(mainChannelWriter != null);

            canJoin = false; //不允许加入服务器
            mainChannelWriter.Write(-1); //告诉服务器开始游戏
        }

        public MultiplexingStream.Channel AllocNewChannel()
        {
            Debug.Assert(multiplexingStream != null);
            Debug.Assert(mainChannelWriter != null);

            if(!canJoin)
            {
                throw new InvalidOperationException();
            }

            var channel = multiplexingStream.CreateChannel();
            mainChannelWriter.Write((int)channel.QualifiedId.Id);
            return channel;
        }

        protected override void MyDispose()
        {
            inPipe?.Dispose();
            outPipe?.Dispose();

            try
            {
                serverProcess?.Kill();
            }
            catch { }
        }
    }
}
