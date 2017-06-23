using System;
using System.IO;

namespace RemoteSync
{
    public class DotnetFileSystemWatcher : IFileSystemWatcher
    {
        private FileSystemWatcher fsw;

        public DotnetFileSystemWatcher(string directory)
        {
            fsw = new FileSystemWatcher
            {
                Path = directory,
                IncludeSubdirectories = true,
            };
        }

        public void Dispose()
        {
            fsw.Dispose();
        }

        public string WaitForChanged()
        {
            var name = fsw.WaitForChanged(WatcherChangeTypes.All).Name;
            return Path.Combine(fsw.Path, name);
        }
    }
}
