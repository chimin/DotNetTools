using System;

namespace RemoteSync
{
    interface ISyncClient : IDisposable
    {
        void Test();
        void Upload(string sourceFile, string targetFile);
        long? GetFileSize(string targetFile);
        DateTime? GetFileTimestamp(string targetFile);
        void Close();
    }

    interface ISyncClientFactory
    {
        ISyncClient Create(string target);
    }
}