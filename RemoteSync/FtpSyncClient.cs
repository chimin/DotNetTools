using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace RemoteSync
{
    class FtpSyncClient : ISyncClient
    {
        private FluentFTP.FtpClient ftp;
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

            ftp = new FluentFTP.FtpClient
            {
                Host = host,
                Credentials = new System.Net.NetworkCredential(user, password),
            };
        }

        public void Dispose()
        {
            ftp.Dispose();
        }

        public void Test()
        {
            ftp.Connect();
        }

        private Stream OpenWrite(string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            if (!ftp.IsConnected)
            {
                ftp.Connect();
            }

            try
            {
                return ftp.OpenWrite(targetFile);
            }
            catch (FluentFTP.FtpException)
            {
                var items = targetFile.Split('/');
                var targetDirectory = "";
                foreach (var i in items.Take(items.Length - 1))
                {
                    targetDirectory = targetDirectory + "/" + i;
                    if (!ftp.DirectoryExists(targetDirectory))
                    {
                        ftp.CreateDirectory(targetDirectory);
                    }
                }
                return ftp.OpenWrite(targetFile);
            }
        }

        public void Upload(string sourceFile, string targetFile)
        {
            using (var source = File.OpenRead(sourceFile))
            using (var target = OpenWrite(targetFile))
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

            if (!ftp.IsConnected)
            {
                ftp.Connect();
            }

            var fileSize = ftp.GetFileSize(targetFile);
            return fileSize;
        }

        public void Close()
        {
            ftp.Disconnect();
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
