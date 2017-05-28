using System;

namespace RemoteSync
{
    interface ISyncClient : IDisposable
    {
        void Test();
        void Upload(string sourceFile, string targetFile);
        SyncFileInfo GetFileInfo(string targetFile);
        void Close();
    }

    class SyncFileInfo
    {
        public bool Exists;
        public long? Size;
        public DateTime? Timestamp;
    }

    interface ISyncClientFactory
    {
        ISyncClient Create(string target);
    }
}