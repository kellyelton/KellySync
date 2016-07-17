using System;
using System.Diagnostics;
using System.IO;

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
        private bool _isDirectory;

        private System.Collections.Generic.Dictionary<string, WatcherChangeTypes> _inProgress;


        private static readonly string HomePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%");

        public FileSync( Config config, bool isDirectory, string path ) : this(config, isDirectory, path, null) { }

        public FileSync( Config config, bool isDirectory, string path, string filter ) {
            _config = config;
            _isDirectory = isDirectory;
            _inProgress = new System.Collections.Generic.Dictionary<string, WatcherChangeTypes>(StringComparer.InvariantCultureIgnoreCase);
            RemotePath = GetRemotePath(path, _isDirectory);
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
            _localHandler.Start();
            _remoteHandler.Start();
        }

        public void Stop() {
            if (disposedValue) throw new ObjectDisposedException(nameof(FileSync));
            _localHandler.Stop();
            _remoteHandler.Stop();
        }

        private void OnLocalFileEvent( object sender, FileSystemEventArgs args ) {
            var toPath = GetRemotePath(args.FullPath, false);
            var fromPath = args.FullPath;
            ReplicateFileEvent(fromPath, toPath, args);
        }

        private void OnRemoteFileEvent( object sender, FileSystemEventArgs args ) {
            var toPath = GetLocalPath(args.FullPath, false);
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
                    var before = isToRemote ? GetRemotePath(rargs.OldFullPath, false) : GetLocalPath(rargs.OldFullPath, false);
                    var after = isToRemote ? GetRemotePath(rargs.FullPath, false) : GetLocalPath(rargs.FullPath, false);
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

        private string GetRemotePath( string fullPath, bool isDirectory ) {
            return Path.Combine(GetPath(_config.FileDumpPath), Slashify(GetPath(fullPath), isDirectory));
        }
        private string GetLocalPath( string fullPath, bool isDirectory ) {
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

        private static string Slashify( string path, bool isDirectory ) {
            FileSystemInfo fi = isDirectory 
                ? (FileSystemInfo)(new DirectoryInfo(path))
                : new FileInfo(path);

            return fi.FullName.Replace(":", "");
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
