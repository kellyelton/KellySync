using System;
using System.Collections.Generic;

namespace KellySync
{
    /// <summary>
    /// Monitors all the files and fires certain actions based on the files changed
    /// </summary>
    public class MonitorProcess
    {
        public Config Config { get; set; }
        public IOEventHandlerBase[] IOEventHandlers => _ioEventHandlers.ToArray();
        private List<IOEventHandlerBase> _ioEventHandlers;
    }
}
