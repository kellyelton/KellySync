using System.IO;

namespace KellySync
{
    public class Config
    {
        public string RootPath { get; set; }
        public string ApplicationPath => Path.Combine(RootPath, "App");
        public string FileDumpPath => Path.Combine(RootPath, "Files");
    }
}