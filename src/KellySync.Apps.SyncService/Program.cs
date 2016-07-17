using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace KellySync
{
    class Program
    {
        static log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main( string[] args ) {
            Trace.Listeners.Add(new ConsoleTraceListener());
            Run().Wait();
        }

        static async Task Run() {
            var config = new Config() {
                RootPath = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\OneDrive\KellySync")
            };
            using (var service = new KellySyncService(config)) {
                service.RegisterFileSyncs(GetFileSyncs(config));

                await Task.WhenAny(Task.Run(() => Console.ReadLine()), service.Run());
            }
        }

        static IEnumerable<FileSync> GetFileSyncs( Config config ) {
            yield return new FileSync(config, true, @"%USERPROFILE%\SyncTest");
        }
    }
}
