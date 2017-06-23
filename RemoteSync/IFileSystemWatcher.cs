using System;
namespace RemoteSync
{
    public interface IFileSystemWatcher : IDisposable
    {
        string WaitForChanged();
    }
}
