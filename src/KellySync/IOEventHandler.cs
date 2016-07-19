using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace KellySync
{
    public class IOEventHandler : IDisposable
    {
        public FileSystemWatcher Watcher { get; private set; }
        public WatcherChangeTypes EventsToHandle { get; }

        private ConcurrentDictionary<WatcherChangeTypes, ConcurrentBag<FileSystemEventHandler>> _handlers;

        public IOEventHandler( string path )
            : this(path, null, WatcherChangeTypes.All) {
        }
        public IOEventHandler( string path, string filter )
            : this(path, filter, WatcherChangeTypes.All) {
        }
        public IOEventHandler( string path, WatcherChangeTypes eventsToHandle )
            : this(path, null, eventsToHandle) {
        }
        public IOEventHandler( string path, string filter, WatcherChangeTypes eventsToHandle ) {
            if (filter != null)
                Watcher = new FileSystemWatcher(path, filter);
            else
                Watcher = new FileSystemWatcher(path);
            .NotifyFilter = NotifyFilters.FileName; <---- The key to all things
            EventsToHandle = eventsToHandle;
            _handlers = new ConcurrentDictionary<WatcherChangeTypes, ConcurrentBag<FileSystemEventHandler>>();

            if (EventsToHandle.HasFlag(WatcherChangeTypes.Created)) {
                Watcher.Created += OnFileCreatedFired;
            }
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Changed)) {
                Watcher.Changed += OnFileChangedFired;
            }
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Deleted)) {
                Watcher.Deleted += OnFileDeletedFired;
            }
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Renamed)) {
                Watcher.Renamed += OnFileRenamedFired;
            }

        }

        public IOEventHandler OnFileCreated( FileSystemEventHandler action ) {
            return On(WatcherChangeTypes.Created, action);
        }
        public IOEventHandler OnFileChanged( FileSystemEventHandler action ) {
            return On(WatcherChangeTypes.Changed, action);
        }
        public IOEventHandler OnFileDeleted( FileSystemEventHandler action ) {
            return On(WatcherChangeTypes.Deleted, action);
        }
        public IOEventHandler OnFileRenamed( FileSystemEventHandler action ) {
            return On(WatcherChangeTypes.Renamed, action);
        }

        public IOEventHandler On( FileSystemEventHandler action ) => On(WatcherChangeTypes.All, action);

        public IOEventHandler On( WatcherChangeTypes eveTypes, FileSystemEventHandler action ) {
            if (eveTypes.HasFlag(WatcherChangeTypes.Created)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Created, x => new ConcurrentBag<FileSystemEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Changed)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Changed, x => new ConcurrentBag<FileSystemEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Deleted)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Deleted, x => new ConcurrentBag<FileSystemEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Renamed)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Renamed, x => new ConcurrentBag<FileSystemEventHandler>());
                list.Add(action);
            }

            return this;
        }

        protected virtual void OnFileCreatedFired( object sender, FileSystemEventArgs e ) {
            OnFileEventFired(WatcherChangeTypes.Created, sender, e);
        }
        protected virtual void OnFileChangedFired( object sender, FileSystemEventArgs e ) {
            OnFileEventFired(WatcherChangeTypes.Changed, sender, e);
        }
        protected virtual void OnFileDeletedFired( object sender, FileSystemEventArgs e ) {
            OnFileEventFired(WatcherChangeTypes.Deleted, sender, e);
        }
        protected virtual void OnFileRenamedFired( object sender, FileSystemEventArgs e ) {
            OnFileEventFired(WatcherChangeTypes.Renamed, sender, e);
        }

        protected virtual void OnFileEventFired( WatcherChangeTypes eveType, object sender, FileSystemEventArgs e ) {
            var list = _handlers.GetOrAdd(eveType, x => new ConcurrentBag<FileSystemEventHandler>());
            var exceptions = new List<Exception>();
            foreach (var item in list.ToArray()) {
                try {
                    item(sender, e);
                } catch (Exception ex) {
                    exceptions.Add(ex);
                }
            }
            if (exceptions.Count == 0) return;
            else if (exceptions.Count == 1) throw exceptions[0];
            else throw new AggregateException(exceptions);
        }

        public void Start() {
            Watcher.EnableRaisingEvents = true;
        }

        public void Stop() {
            Watcher.EnableRaisingEvents = false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    Watcher.Dispose();
                    Watcher = null;
                    while (_handlers.Count > 0) {
                        foreach (var key in _handlers.Keys) {
                            ConcurrentBag<FileSystemEventHandler> item = null;
                            if (!_handlers.TryGetValue(key, out item)) continue;

                            while (item.Count > 0) {
                                FileSystemEventHandler h;
                                if (!item.TryTake(out h)) Thread.Yield();
                            }
                            _handlers.TryRemove(key, out item);
                        }
                    }
                    _handlers.Clear();
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