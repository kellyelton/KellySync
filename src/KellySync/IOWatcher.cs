using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace KellySync
{
    public class IOWatcher : IDisposable
    {
        public FileSystemWatcher Watcher { get; private set; }
        public WatcherChangeTypes EventsToHandle { get; }

        private ConcurrentDictionary<WatcherChangeTypes, ConcurrentBag<IOEventHandler>> _handlers;

        public IOWatcher( string path )
            : this(path, null, WatcherChangeTypes.All) {
        }
        public IOWatcher( string path, string filter )
            : this(path, filter, WatcherChangeTypes.All) {
        }
        public IOWatcher( string path, WatcherChangeTypes eventsToHandle )
            : this(path, null, eventsToHandle) {
        }
        public IOWatcher( string path, string filter, WatcherChangeTypes eventsToHandle ) {
			if(filter == null) { // It's a directory
				Watcher = new FileSystemWatcher(path);
			} else Watcher = new FileSystemWatcher(path, filter);

			Watcher.NotifyFilter = NotifyFilters.DirectoryName | Watcher.NotifyFilter | NotifyFilters.FileName | NotifyFilters.Attributes | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size;

            EventsToHandle = eventsToHandle;
            _handlers = new ConcurrentDictionary<WatcherChangeTypes, ConcurrentBag<IOEventHandler>>();

            if (EventsToHandle.HasFlag(WatcherChangeTypes.Created))
                Watcher.Created += OnFileSystemEventFired;
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Changed))
                Watcher.Changed += OnFileSystemEventFired;
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Deleted))
                Watcher.Deleted += OnFileSystemEventFired;
            if (EventsToHandle.HasFlag(WatcherChangeTypes.Renamed))
                Watcher.Renamed += OnFileSystemEventFired;

        }

        protected virtual void OnFileSystemEventFired( object sender, FileSystemEventArgs e ) {
			switch (e.ChangeType) {
				case WatcherChangeTypes.Created:
					OnIOEventFired(WatcherChangeTypes.Created, this, e);
					break;
				case WatcherChangeTypes.Deleted:
					OnIOEventFired(WatcherChangeTypes.Deleted, this, e);
					break;
				case WatcherChangeTypes.Changed:
					OnIOEventFired(WatcherChangeTypes.Changed, this, e);
					break;
				case WatcherChangeTypes.Renamed:
					OnIOEventFired(WatcherChangeTypes.Renamed, this, e);
					break;
			}
		}

        protected virtual void OnIOEventFired( WatcherChangeTypes eveType, object sender, FileSystemEventArgs e ) {
            var list = _handlers.GetOrAdd(eveType, x => new ConcurrentBag<IOEventHandler>()).ToArray();
            var exceptions = new List<Exception>(list.Length);
			var args = new IOEventArgs(e);
            foreach (var item in list) {
                try {
                    item(sender, args);
                } catch (Exception ex) {
                    exceptions.Add(ex);
                }
            }
            if (exceptions.Count == 0) return;
            else if (exceptions.Count == 1) throw exceptions[0];
            else throw new AggregateException(exceptions);
        }

        public IOWatcher OnCreated( IOEventHandler action ) => On(WatcherChangeTypes.Created, action);
        public IOWatcher OnChanged( IOEventHandler action ) => On(WatcherChangeTypes.Changed, action);
        public IOWatcher OnDeleted( IOEventHandler action ) => On(WatcherChangeTypes.Deleted, action);
        public IOWatcher OnRenamed( IOEventHandler action ) => On(WatcherChangeTypes.Renamed, action);

        public IOWatcher On( IOEventHandler action ) => On(WatcherChangeTypes.All, action);

        public IOWatcher On( WatcherChangeTypes eveTypes, IOEventHandler action ) {
            if (eveTypes.HasFlag(WatcherChangeTypes.Created)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Created, x => new ConcurrentBag<IOEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Changed)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Changed, x => new ConcurrentBag<IOEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Deleted)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Deleted, x => new ConcurrentBag<IOEventHandler>());
                list.Add(action);
            }
            if (eveTypes.HasFlag(WatcherChangeTypes.Renamed)) {
                var list = _handlers.GetOrAdd(WatcherChangeTypes.Renamed, x => new ConcurrentBag<IOEventHandler>());
                list.Add(action);
            }

            return this;
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
                            ConcurrentBag<IOEventHandler> item = null;
                            if (!_handlers.TryGetValue(key, out item)) continue;

                            while (item.Count > 0) {
								IOEventHandler h;
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

	public delegate void IOEventHandler( object sender, IOEventArgs args);
}