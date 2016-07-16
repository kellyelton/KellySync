using System.Collections.Generic;

namespace KellySync
{
    public class ApplicationUpdatedIOEventHandler : IOEventHandlerBase
    {
        public ApplicationUpdatedIOEventHandler( Config config ) : base(config) {
        }

        protected override IEnumerable<WatcherSettings> GetWatchers( Config config ) {
            yield return new WatcherSettings(config.ApplicationPath, "*.*", IOEventType.Created | IOEventType.Deleted | IOEventType.Modified | IOEventType.Renamed);
        }
    }
}