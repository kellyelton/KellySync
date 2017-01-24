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
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Trace.Listeners.Add(new ConsoleTraceListener());
            Run().Wait();
        }

        private static void CurrentDomain_UnhandledException( object sender, UnhandledExceptionEventArgs e ) {
            //throw new NotImplementedException();
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
            yield return new FileSync(config, @"%USERPROFILE%\SyncTest");
			yield return new FileSync( config, @"%USERPROFILE%\vimfiles" );
			yield return new FileSync( config, @"%USERPROFILE%", ".bashrc" );
			yield return new FileSync( config, @"%USERPROFILE%", ".gitconfig" );
			yield return new FileSync( config, @"%USERPROFILE%", ".vimrc" );
		}
    }
}
