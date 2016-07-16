using System;
using System.IO;

namespace KellySync
{
    public class WatcherSettings : IDisposable
    {
        public FileSystemWatcher Watcher { get; }
        public IOEventType EventsToHandle { get; }

        public WatcherSettings( string path )
            : this(path, null, IOEventType.Created | IOEventType.Deleted | IOEventType.Modified | IOEventType.Renamed) {
        }
        public WatcherSettings( string path, string filter )
            : this(path, filter, IOEventType.Created | IOEventType.Deleted | IOEventType.Modified | IOEventType.Renamed) {
        }
        public WatcherSettings( string path, IOEventType eventsToHandle )
            : this(path, null, eventsToHandle) {
        }
        public WatcherSettings( string path, string filter, IOEventType eventsToHandle ) {
            if (filter != null)
                Watcher = new FileSystemWatcher(path, filter);
            else
                Watcher = new FileSystemWatcher(path);
            EventsToHandle = eventsToHandle;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
                    Watcher.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~WatcherSettings() {
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