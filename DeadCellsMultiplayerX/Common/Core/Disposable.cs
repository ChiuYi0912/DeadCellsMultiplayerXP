using Microsoft;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Core
{
    public abstract class Disposable : IDisposable, IDisposableObservable
    {
        private readonly CancellationTokenSource disposedSource = new();

        public CancellationToken DisposeToken => disposedSource.Token;
        public bool IsDisposed => disposedSource.IsCancellationRequested;
        public virtual ILogger Logger { get; } 
        protected abstract void MyDispose();

        public Disposable()
        {
            Logger = Log.ForContext(GetType());
        }

        public void Dispose()
        {
            if(IsDisposed)
            {
                return;
            }

            GC.SuppressFinalize(this);
            disposedSource.Cancel();

            MyDispose();
        }

        ~Disposable()
        {
            Dispose();
        }
    }
}
