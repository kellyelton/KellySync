using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace KellySync
{
    public abstract class IOEventHandlerBase : IDisposable
    {
        private WatcherSettings[] _watchers;
        public IOEventHandlerBase( Config config ) {
            _watchers = GetWatchers(config).ToArray();
            foreach (var watcher in _watchers) {
                watcher.Watcher.Changed += OnFileChanged;
                watcher.Watcher.Created += OnFileCreated;
                watcher.Watcher.Deleted += OnFileDeleted;
                watcher.Watcher.Renamed += OnFileRenamed;
            }
        }


        protected virtual void OnFileChanged( object sender, FileSystemEventArgs e ) { }
        protected virtual void OnFileCreated( object sender, FileSystemEventArgs e ) { }
        protected virtual void OnFileDeleted( object sender, FileSystemEventArgs e ) { }
        protected virtual void OnFileRenamed( object sender, FileSystemEventArgs e ) { }

        public void Start() {
            foreach (var w in _watchers) {
                w.Watcher.EnableRaisingEvents = true;
            }
        }

        public void Stop() {
            foreach (var w in _watchers) {
                w.Watcher.EnableRaisingEvents = false;
            }
        }

        protected abstract IEnumerable<WatcherSettings> GetWatchers( Config config );

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    foreach (var watcher in _watchers) {
                        watcher.Dispose();
                    }
                    _watchers = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~IOEventHandlerBase() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }

}