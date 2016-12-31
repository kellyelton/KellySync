using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KellySync
{
    public class KellySyncService : IDisposable {
        public Config Config { get; }

		/// <summary>
		/// Returns a copy of the list of <see cref="FileSync"/>'s
		/// </summary>
        public FileSync[] FileSyncs { get { lock (_fileSyncs) return _fileSyncs.ToArray(); }}

        private List<FileSync> _fileSyncs;
        private ManualResetEventSlim _running;

        public KellySyncService( Config config ) {
            Config = config;
            _fileSyncs = new List<FileSync>();
            _running = new ManualResetEventSlim();
        }

        public void RegisterFileSync(FileSync handler ) {
            lock(_fileSyncs)
                _fileSyncs.Add(handler);
        }

        public void RegisterFileSyncs( IEnumerable<FileSync> syncs ) {
            lock (_fileSyncs) {
                foreach (var sync in syncs)
                    _fileSyncs.Add(sync);
            }
        }

        public async Task Run() {
            lock (_fileSyncs) {
                foreach(var sync in _fileSyncs) {
                    sync.Start();
                }
            }
            await Task.Run(() =>_running.Wait());
        }

        public void Stop() {
            lock (_fileSyncs) {
                foreach (var sync in _fileSyncs) {
                    sync.Stop();
                }
            }
            _running.Set();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    foreach(var sync in FileSyncs) {
                        sync.Stop();
                        sync.Dispose();
                        _fileSyncs.Remove(sync);
                    }
               }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~KellySyncService() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
