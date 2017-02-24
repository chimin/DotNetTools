﻿using System;

namespace RemoteSync
{
    interface ISyncClient : IDisposable
    {
        void Test();
        void Upload(string sourceFile, string targetFile);
        long GetFileSize(string targetFile);
        void Close();
    }
}