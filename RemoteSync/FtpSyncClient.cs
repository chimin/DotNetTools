using System;
using System.IO;
using System.Text.RegularExpressions;
using ArxOne.Ftp;

namespace RemoteSync
{
    class FtpSyncClient : ISyncClient
    {
        private ArxOne.Ftp.FtpClient ftp;
        private string user;
        private string password;
        private string host;
        private string directory;

        public FtpSyncClient(string target)
        {
            var pattern = new Regex("^(.*)@(.*):([^/]*)/(.*)$");
            var match = pattern.Match(target);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid target " + target);
            }

            user = match.Groups[1].Value;
            password = match.Groups[2].Value;
            host = match.Groups[3].Value;
            directory = match.Groups[4].Value;        
        }

        public void Dispose()
        {
            Close();
        }

        private ArxOne.Ftp.FtpClient GetFtpClient()
        {
            if (ftp == null)
            {
                ftp = new ArxOne.Ftp.FtpClient(ArxOne.Ftp.FtpProtocol.Ftp,
                                               host, 21,
                                               new System.Net.NetworkCredential(user, password),
                                               new FtpClientParameters
                                               {
                                                   Passive = true,
                                               });
            }
            return ftp;
        }

        public void Test() { }

        public void Upload(string sourceFile, string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            using (var source = File.OpenRead(sourceFile))
            using (var target = GetFtpClient().Stor(new FtpPath(targetFile)))
            {
                source.CopyTo(target);
            }
        }

        public long GetFileSize(string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            var ftpEntry = GetFtpClient().GetEntry(new FtpPath(targetFile));
            return ftpEntry?.Size ?? 0;
        }

        public void Close()
        {
            if (ftp != null)
            {
                ftp.Dispose();
                ftp = null;
            }
        }
    }

    class FtpSyncClientFactory : ISyncClientFactory
    {
        public ISyncClient Create(string target)
        {
            return new FtpSyncClient(target);
        }
    }
}
