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
        public string RemotePath { get; }
        public string LocalPath { get; }
        public string FullRemotePath { get; }
        public string FullLocalPath { get; }

        private IOEventHandler _localHandler;
        private IOEventHandler _remoteHandler;
        private Config _config;
        private CancellationTokenSource _scanTaskCancel;
        private Task _scanTask;
        private string _filter;

        private Dictionary<string, WatcherChangeTypes> _inProgress;


        private static readonly string HomePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%");

        public FileSync( Config config, string path ) : this(config, path, null) { }

        public FileSync( Config config, string path, string filter ) {
            _config = config;
            _filter = filter;
            if (string.IsNullOrWhiteSpace(_filter)) _filter = "*";
            _inProgress = new System.Collections.Generic.Dictionary<string, WatcherChangeTypes>(StringComparer.InvariantCultureIgnoreCase);
            RemotePath = GetRemotePath(path);
            LocalPath = GetPath(path);

            if (!Directory.Exists(LocalPath)) {
                Trace.WriteLine($"Creating Directory: '{LocalPath}'");
                Directory.CreateDirectory(LocalPath);
            }
            if (!Directory.Exists(RemotePath)) {
                Trace.WriteLine($"Creating Directory: '{RemotePath}'");
                Directory.CreateDirectory(RemotePath);
            }

            FullRemotePath = RemotePath;
            FullLocalPath = LocalPath;
            if (!string.IsNullOrWhiteSpace(filter)) {
                FullRemotePath = FullRemotePath + $"\\{filter}";
                FullLocalPath = FullLocalPath + $"\\{filter}";
            }

            _localHandler = new IOEventHandler(LocalPath, filter).On(OnLocalFileEvent);
            _remoteHandler = new IOEventHandler(RemotePath, filter).On(OnRemoteFileEvent);
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
                        var path = en.Current;
                        var opath = GetOppositePath(path);
                        lock (this._inProgress) {
                            // This way we don't tickle files that are being moved
                            if (this._inProgress.ContainsKey(path) || this._inProgress.ContainsKey(opath)) continue;
                            SyncFile(path, GetOppositePath(path));
                        }
                        await Task.Delay(100, cancel);
                    }
                }
                await Task.Delay(30000, cancel);
            }
        }

        private void SyncFile( string a, string b ) {
            if (!File.Exists(a) && File.Exists(b)) {
                File.Copy(b, a, true);
                File.SetLastWriteTime(b, File.GetLastWriteTime(a));
                return;
            }
            if (File.Exists(a) && !File.Exists(b)) {
                File.Copy(a, b, true);
                File.SetLastWriteTime(a, File.GetLastWriteTime(b));
                return;
            }

            var atime = File.GetLastWriteTime(a);
            var btime = File.GetLastWriteTime(b);

            if (atime > btime) {
                File.Copy(a, b, true);
                File.SetLastWriteTime(a, File.GetLastWriteTime(b));
                return;
            }
            if (btime > atime) {
                File.Copy(b, a, true);
                File.SetLastWriteTime(b, File.GetLastWriteTime(a));
                return;
            }
        }

        private void OnLocalFileEvent( object sender, bool isDirectory, FileSystemEventArgs args ) {
            var directory = args.FullPath;
            if (!isDirectory) directory = Directory.GetParent(args.FullPath).FullName;
            var toPath = GetRemotePath(directory);
            var fromPath = directory;
            ReplicateFileEvent(fromPath, toPath, args);
        }

        private void OnRemoteFileEvent( object sender, bool isDirectory, FileSystemEventArgs args ) {
            var toPath = GetLocalPath(args.FullPath);
            var fromPath = args.FullPath;
            ReplicateFileEvent(fromPath, toPath, args);
        }

        private void ReplicateFileEvent( string fromPath, string toPath, FileSystemEventArgs args ) {
            if (disposedValue) return;
            lock (_inProgress) {
                if (args.ChangeType == WatcherChangeTypes.Renamed) {
                    var r = (RenamedEventArgs)args;
                    Trace.WriteLine($"File Event: {args.ChangeType} '{r.OldFullPath}' to '{r.FullPath}'");
                } else {
                    Trace.WriteLine($"File Event: {args.ChangeType} '{args.FullPath}'");
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
                    var rargs = (RenamedEventArgs)args;
                    var isToRemote = rargs.OldFullPath.Contains(new DirectoryInfo(LocalPath).FullName);
                    var before = isToRemote ? GetRemotePath(rargs.OldFullPath) : GetLocalPath(rargs.OldFullPath);
                    var after = isToRemote ? GetRemotePath(rargs.FullPath) : GetLocalPath(rargs.FullPath);
                    if (File.Exists(before)) {
                        Trace.WriteLine($"Moving File: '{before}' -> '{after}'");
                        File.Move(before, after);
                    } else {
                        before = isToRemote ? toPath : fromPath;
                        Trace.WriteLine($"Copying File: '{before}' -> '{after}'");
                        File.Copy(before, after, true);
                    }
                    break;
                }
            }
        }

        private IEnumerable<string> GetPathsToScan() {
            if (!this._filter.Contains("*")) {
                // If it's just a file, then return just the Local and Remote paths
                yield return this.FullRemotePath;
                yield return this.FullLocalPath;
            } else {
                // If it's a directory, use GetFiles(filter) on the Local and Remote paths
                foreach (var file in Directory.GetFiles(this.RemotePath, this._filter)) {
                    yield return file;
                }
                foreach (var file in Directory.GetFiles(this.LocalPath, this._filter)) {
                    yield return file;
                }
            }
        }

        private string GetOppositePath( string fullPath ) {
            if (fullPath.Contains(this.RemotePath))
                return GetLocalPath(fullPath);
            return GetRemotePath(fullPath);
        }

        private string GetRemotePath( string fullPath ) {
            return Path.Combine(GetPath(_config.FileDumpPath), Slashify(GetPath(fullPath)));
        }
        private string GetLocalPath( string fullPath ) {
            var path = fullPath.Replace(GetPath(_config.FileDumpPath), "");
            path = path.Substring(1);
            path = path.Insert(1, ":");
            return path;
        }

        private static string GetPath( string path ) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            var ret = path;

            try {
                // Remove ending slash
                ret = ret.TrimEnd('\\', '/');

                // ~ for home
                ret = ret.Replace("~", HomePath);

                // Expand env vars
                ret = Environment.ExpandEnvironmentVariables(ret);

                if (!System.IO.Path.IsPathRooted(ret))
                    throw new FormatException($"No root defined for path '{path}'.");

                return ret;
            } catch (Exception ex) {
                throw new FormatException($"Can't resolve path: '{path}' -> '{ret}'", ex);
            }
        }

        private static string Slashify( string path ) {
            return new DirectoryInfo(path).FullName.Replace(":", "");
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
