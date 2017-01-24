using System;
using System.Diagnostics;
using System.IO;

namespace KellySync
{
    public class FilePath
    {
        public string OriginalPath { get; }
        public string LocalPath { get; }
        public string RemotePath { get; }
        public bool IsDirectory { get; }

        private static readonly string HomePath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%");

        private Config _config;

        public FilePath( string path, Config config ) {
            _config = config;
            if (IsRemotePath(path) == false) {
                OriginalPath = path;
                LocalPath = ExpandCleanValidatePath(OriginalPath);
                RemotePath = GetRemotePath(LocalPath);
            } else {
                OriginalPath = GetOriginalPath(path);
                LocalPath = GetLocalPath(path);
                RemotePath = ExpandCleanValidatePath(path, expand: false);
            }

            if (File.Exists(LocalPath) || File.Exists(RemotePath)) IsDirectory = false;
            else if (Directory.Exists(LocalPath) || Directory.Exists(RemotePath)) IsDirectory = true;
            else throw new InvalidOperationException();
        }

        public void CreateDirectories() {
            if (!Directory.Exists(LocalPath)) {
                Trace.WriteLine($"Creating Directory: '{LocalPath}'");
                Directory.CreateDirectory(LocalPath);
            }
            if (!Directory.Exists(RemotePath)) {
                Trace.WriteLine($"Creating Directory: '{RemotePath}'");
                Directory.CreateDirectory(RemotePath);
            }
        }

        private string GetRemotePath( string fullPath ) {
            return Path.Combine(ExpandCleanValidatePath(_config.FileDumpPath), Slashify(ExpandCleanValidatePath(fullPath)));
        }

        private bool IsRemotePath( string path ) {
            var cpath = ExpandCleanValidatePath(path);

            return cpath.StartsWith(_config.FileDumpPath, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetOriginalPath( string fullPath ) {
            var path = fullPath.Substring(ExpandCleanValidatePath(_config.FileDumpPath).Length);
            if (path[0] == '%')
                return path;
            path = path.Insert(1, ":");
            return path;
        }

        private string GetLocalPath( string fullPath ) {
            var path = ExpandCleanValidatePath(GetOriginalPath(fullPath));
            return path;
        }

        //private string GetOppositePath( string fullPath ) {
        //    if (fullPath.Contains(this.RemotePath))
        //        return GetLocalPath(fullPath);
        //    return GetRemotePath(fullPath);
        //}

        private static string Slashify( string path ) {
            return new DirectoryInfo(path).FullName.Replace(":", "");
        }

        private static string ExpandCleanValidatePath( string path, bool expand = true) {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentNullException(nameof(path));
            var ret = path;

            try {
                // Remove ending slash
                ret = ret.TrimEnd('\\', '/');

                // ~ for home
                ret = ret.Replace("~", HomePath);

                // Expand env vars
                if(expand)
                    ret = Environment.ExpandEnvironmentVariables(ret);

                if (!System.IO.Path.IsPathRooted(ret))
                    throw new FormatException($"No root defined for path '{path}'.");

                return ret;
            } catch (Exception ex) {
                throw new FormatException($"Can't resolve path: '{path}' -> '{ret}'", ex);
            }
        }
    }
}