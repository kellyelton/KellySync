using System;
using System.IO;

namespace KellySync
{
	public class IOEventArgs : EventArgs
	{
		public WatcherChangeTypes ChangeType { get; }
		public string FullPath { get; }
		public string Name { get; }
        /// <summary>
        /// If <see cref="FullPath"/> is a directory. The only time this should be null is if 
        /// the <see cref="ChangeType"/> is <see cref="WatcherChangeTypes.Deleted"/> OR
        /// if the file/directory got deleted before we could determin what it was.
        /// </summary>
		public bool? IsDirectory { get; }
		public bool IsRename { get; }
		public string OldFullPath { get; }
		public string OldName { get; }

		public IOEventArgs( FileSystemEventArgs fileSystemArgs ) {
			IsDirectory = IsPathDirectory(FullPath);
			ChangeType = fileSystemArgs.ChangeType;
			FullPath = fileSystemArgs.FullPath;
			Name = fileSystemArgs.Name;
			var rename = fileSystemArgs as RenamedEventArgs;
			if (rename != null) {
				IsRename = true;
				OldName = rename.OldName;
				OldFullPath = rename.OldFullPath;
			}
		}

		private static bool? IsPathDirectory( string path ) {
			try {
				var attr = File.GetAttributes(path);
				return attr.HasFlag(FileAttributes.Directory);
			} catch (FileNotFoundException) {
			} catch (DirectoryNotFoundException) {
			}
			return null;
		}
	}
}