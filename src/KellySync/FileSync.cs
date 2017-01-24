using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace KellySync
{
    public class FileSync : IDisposable
    {
        public FilePath Path { get; }

        private IOWatcher _localHandler;
        private IOWatcher _remoteHandler;
        private Config _config;
        private CancellationTokenSource _scanTaskCancel;
        private Task _scanTask;
        private string _filter;

        private Dictionary<string, WatcherChangeTypes> _inProgress;

        public FileSync( Config config, string path ) : this(config, path, null) { }

        public FileSync( Config config, string path, string filter ) {
            _config = config;
            _filter = filter;
            if (string.IsNullOrWhiteSpace(_filter)) _filter = "*";
            _inProgress = new System.Collections.Generic.Dictionary<string, WatcherChangeTypes>(StringComparer.InvariantCultureIgnoreCase);
            Path = new FilePath(path, _config);
            Path.CreateDirectories();

            _localHandler = new IOWatcher(_config, Path.LocalPath, filter).On(OnLocalFileEvent).On(OnLocalIOEventException);
            _remoteHandler = new IOWatcher(_config, Path.RemotePath, filter).On(OnRemoteFileEvent).On(OnRemoteIOEventException);
        }

        public void Start() {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileSync));
            if (_scanTaskCancel != null) return;
            _scanTaskCancel = new CancellationTokenSource();
            _scanTask = Scan(_scanTaskCancel.Token);
            _localHandler.Start();
            _remoteHandler.Start();
        }

        public void Stop() {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileSync));
            if (_scanTaskCancel == null) return;
            _localHandler.Stop();
            _remoteHandler.Stop();

            var loc = _scanTaskCancel;
            _scanTaskCancel = null;
            loc.Cancel();
            _scanTask.Wait();
            loc.Dispose();
            _scanTask.Dispose();
            _scanTask = null;
        }

        private async Task Scan( CancellationToken cancel ) {
            var query = GetPathsToScan();
            while (!disposedValue && !cancel.IsCancellationRequested) {
                using (var en = query.GetEnumerator()) {
                    while (en.MoveNext() && !cancel.IsCancellationRequested) {
                        // TODO: actually do scanning on file
                        var path = new FilePath(en.Current, _config);
                        lock (this._inProgress) {
                            // This way we don't tickle files that are being moved
                            if (this._inProgress.ContainsKey(path.OriginalPath)) continue;
                            SyncFilePath(path, _config);
                        }
                        await Task.Delay(100, cancel);
                    }
                }
                await Task.Delay(30000, cancel);
            }
        }

        private void SyncFilePath( FilePath path, Config config ) {
            if (!path.IsDirectory) {
                if (!File.Exists(path.LocalPath) && File.Exists(path.RemotePath)) {
                    File.Copy(path.RemotePath, path.LocalPath, true);
                    File.SetLastWriteTime(path.RemotePath, File.GetLastWriteTime(path.LocalPath));
                    return;
                }
                if (File.Exists(path.LocalPath) && !File.Exists(path.RemotePath)) {
                    File.Copy(path.LocalPath, path.RemotePath, true);
                    File.SetLastWriteTime(path.LocalPath, File.GetLastWriteTime(path.RemotePath));
                    return;
                }

                var atime = File.GetLastWriteTime(path.LocalPath);
                var btime = File.GetLastWriteTime(path.RemotePath);

                if (atime > btime) {
                    File.Copy(path.LocalPath, path.RemotePath, true);
                    File.SetLastWriteTime(path.LocalPath, File.GetLastWriteTime(path.RemotePath));
                    return;
                }
                if (btime > atime) {
                    File.Copy(path.RemotePath, path.LocalPath, true);
                    File.SetLastWriteTime(path.RemotePath, File.GetLastWriteTime(path.LocalPath));
                    return;
                }
            } else {
                if (!Directory.Exists(path.LocalPath) && Directory.Exists(path.RemotePath)) {
                    Directory.CreateDirectory(path.LocalPath);
                    Directory.SetLastWriteTime(path.RemotePath, Directory.GetLastWriteTime(path.LocalPath));
                    return;
                }
                if (Directory.Exists(path.LocalPath) && !Directory.Exists(path.RemotePath)) {
                    Directory.CreateDirectory(path.RemotePath);
                    Directory.SetLastWriteTime(path.LocalPath, Directory.GetLastWriteTime(path.RemotePath));
                    return;
                }

                foreach(var dir in Directory.GetDirectories(path.LocalPath)) {
                    var fpath = new FilePath(dir, config);
                    SyncFilePath(fpath, config);
                }
                foreach(var dir in Directory.GetDirectories(path.RemotePath)) {
                    var fpath = new FilePath(dir, config);
                    SyncFilePath(fpath, config);
                }

                foreach(var file in Directory.GetFiles(path.LocalPath)) {
                    var fpath = new FilePath(file, config);
                    SyncFilePath(fpath, config);
                }
                foreach(var file in Directory.GetFiles(path.RemotePath)) {
                    var fpath = new FilePath(file, config);
                    SyncFilePath(fpath, config);
                }


                var atime = Directory.GetLastWriteTime(path.LocalPath);
                var btime = Directory.GetLastWriteTime(path.RemotePath);

                if (atime > btime) {
                    Directory.SetLastWriteTime(path.LocalPath, Directory.GetLastWriteTime(path.RemotePath));
                } else if (btime > atime) {
                    Directory.SetLastWriteTime(path.RemotePath, Directory.GetLastWriteTime(path.LocalPath));
                }
            }
        }

        private void OnLocalFileEvent( object sender, IOEventArgs args ) {
            ReplicateFileEvent( args.Path.LocalPath, args.Path.RemotePath, false, args );
        }

        private void OnRemoteFileEvent( object sender, IOEventArgs args ) {
            ReplicateFileEvent( args.Path.RemotePath, args.Path.LocalPath, true, args );
        }

        private void ReplicateFileEvent( string fromPath, string toPath, bool isRemoteEvent, IOEventArgs args ) {
            if (disposedValue) return;
            lock (_inProgress) {
                if (args.ChangeType == WatcherChangeTypes.Renamed) {
                    Trace.WriteLine($"File Event: {args.ChangeType} '{args.OldPath.OriginalPath}' to '{args.Path.OriginalPath}'");
                } else {
                    Trace.WriteLine($"File Event: {args.ChangeType} '{args.Path.OriginalPath}'");
                }
                if (_inProgress.ContainsKey(fromPath)) {
                    if (_inProgress[fromPath].HasFlag(args.ChangeType)) {
                        _inProgress[fromPath] = _inProgress[fromPath] & ~args.ChangeType;
                        if (_inProgress[fromPath] == 0)
                            _inProgress.Remove(fromPath);
                        return; // We already triggered this, gtfo
                    }
                }

                if (!_inProgress.ContainsKey(toPath)) _inProgress.Add(toPath, 0);
                _inProgress[toPath] |= args.ChangeType;
            }

            switch (args.ChangeType) {
                case WatcherChangeTypes.Created:
                case WatcherChangeTypes.Changed: {
                    if (!File.Exists(fromPath)) break;

                    var fromLastWrite = File.GetLastWriteTime(fromPath);
                    var toLastWrite = File.GetLastWriteTime(toPath);


                    if (!File.Exists(toPath)) {
                        Trace.WriteLine($"Creating File: '{toPath}'");
                        File.Copy(fromPath, toPath, true);
                        File.SetLastWriteTime(toPath, fromLastWrite);
                        break;
                    }

                    if (fromLastWrite <= toLastWrite) break;


                    Trace.WriteLine($"Replacing File: '{toPath}'");
                    var todir = Directory.GetParent(toPath).FullName;
                    if (!Directory.Exists(todir)) {
                        Trace.WriteLine($"Creating Directory: '{todir}'");
                        Directory.CreateDirectory(todir);
                    }
                    File.Copy(fromPath, toPath, true);
                    File.SetLastWriteTime(toPath, fromLastWrite);
                    break;
                }
                case WatcherChangeTypes.Deleted: {
                    Trace.WriteLine($"Deleting File: '{toPath}'");
                    File.Delete(toPath);
                    break;
                }
                case WatcherChangeTypes.Renamed: {
                    var before = isRemoteEvent ? args.OldPath.LocalPath : args.OldPath.RemotePath;
                    var after = isRemoteEvent ? args.Path.LocalPath : args.Path.RemotePath;

                    if( File.Exists( before ) ) {

                    }

                    if( File.Exists( before ) ) {
                        Trace.WriteLine( $"Moving File: '{before}' -> '{after}'" );
                        File.Move( before, after );
                    } else {
                        before = isRemoteEvent ? fromPath : toPath;
                        Trace.WriteLine( $"Copying File: '{before}' -> '{after}'" );
                        File.Copy( before, after, true );
                    }
                    break;
                }
            }
        }

        private void OnLocalIOEventException( object sender, IOEventException args ) {
            Exception ex = args.InnerException;
            string nl = Environment.NewLine;
            FileSystemEventArgs fa = args.FileSystemArgs;

            Trace.TraceError( $"Local IO [{fa.FullPath}:{fa.ChangeType}] {ex.Message}{nl}{ex.StackTrace}" );
        }

        private void OnRemoteIOEventException( object sender, IOEventException args ) {
            Exception ex = args.InnerException;
            string nl = Environment.NewLine;
            FileSystemEventArgs fa = args.FileSystemArgs;

            Trace.TraceError( $"Remote IO [{fa.FullPath}:{fa.ChangeType}] {ex.Message}{nl}{ex.StackTrace}" );
        }

        private IEnumerable<string> GetPathsToScan() {
            if (!string.IsNullOrWhiteSpace(_filter) && !this._filter.Contains("*")) {
                // If it's just a file, then return just the Local and Remote paths
                yield return this.Path.RemotePath;
                yield return this.Path.LocalPath;
            } else {
                // If it's a directory, use GetFiles(filter) on the Local and Remote paths
                foreach (var file in Directory.GetFiles(this.Path.RemotePath, this._filter)) {
                    yield return file;
                }
                foreach (var file in Directory.GetFiles(this.Path.LocalPath, this._filter)) {
                    yield return file;
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose( bool disposing ) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    Stop();
                    _localHandler.Dispose();
                    _remoteHandler.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~FileSync() {
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
