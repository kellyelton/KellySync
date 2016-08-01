using System;
using System.IO;

namespace KellySync
{
	public class IOEventArgs : EventArgs
	{
		public WatcherChangeTypes ChangeType { get; }
		public string Name { get; }
		public bool IsRename { get; }
        public FilePath Path { get; }
        public FilePath OldPath { get; }

		public IOEventArgs( FileSystemEventArgs fileSystemArgs, Config config ) {
			ChangeType = fileSystemArgs.ChangeType;
			Name = fileSystemArgs.Name;
            Path = new FilePath(fileSystemArgs.FullPath, config);
            
			var rename = fileSystemArgs as RenamedEventArgs;
			if (rename != null) {
				IsRename = true;
                OldPath = new KellySync.FilePath(rename.OldFullPath, config);
			}
		}
	}
}