using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RemoteSync
{
    class ScpSyncClient : ISyncClient
    {
        private Renci.SshNet.SshClient ssh;
        private Renci.SshNet.ScpClient scp;
        private string directory;

        public ScpSyncClient(string target)
        {
            var pattern = new Regex("^(.*)@(.*):(.*)$");
            var match = pattern.Match(target);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid target " + target);
            }

            var user = match.Groups[1].Value;
            var host = match.Groups[2].Value;
            directory = match.Groups[3].Value.TrimEnd('/');

            var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".ssh");
            var keyFilePaths = new List<string>();
            foreach (var i in Directory.EnumerateFiles(keyDirectory))
            {
                if (File.Exists(i + ".pub"))
                {
                    keyFilePaths.Add(i);
                }
            }
            var keyFiles = keyFilePaths.Select(i => new Renci.SshNet.PrivateKeyFile(i)).ToArray();

            ssh = new Renci.SshNet.SshClient(host, user, keyFiles);
            scp = new Renci.SshNet.ScpClient(host, user, keyFiles);
        }

        public void Dispose()
        {
            ssh.Dispose();
            scp.Dispose();
        }

        public void Test()
        {
            if (!ssh.IsConnected)
            {
                ssh.Connect();
            }
        }

        public void Upload(string sourceFile, string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            if (!scp.IsConnected)
            {
                scp.Connect();
            }

            try
            {
                scp.Upload(new FileInfo(sourceFile), targetFile);
            }
            catch (Renci.SshNet.Common.ScpException ex)
            {
                if (!Regex.IsMatch(ex.Message, 
                                   "^scp: .*: set times: Operation not permitted$"))
                {
                    throw;
                }
            }
        }

        private string Stat(string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            if (!ssh.IsConnected)
            {
                ssh.Connect();
            }

            return ssh.RunCommand("stat \"" + targetFile + "\"").Result;
        }

        public long? GetFileSize(string targetFile)
        {
            var stat = Stat(targetFile);
            var match = Regex.Match(stat, @"Size: (\d+)");
            if (match.Success)
            {
                return long.Parse(match.Groups[1].Value);
            }
            else
            {
                return null;
            }
        }

        public DateTime? GetFileTimestamp(string targetFile)
        {
            var stat = Stat(targetFile);
            var match = Regex.Match(stat, @"Modify: (\d+)-(\d+)-(\d+) (\d+):(\d+):(\d+)\.(\d+) ([+-]\d{2})(\d{2})");
            if (match.Success)
            {
                var i = 1;
                var year = int.Parse(match.Groups[i++].Value);
                var month = int.Parse(match.Groups[i++].Value);
                var day = int.Parse(match.Groups[i++].Value);
                var hour = int.Parse(match.Groups[i++].Value);
                var minute = int.Parse(match.Groups[i++].Value);
                var second = int.Parse(match.Groups[i++].Value) + double.Parse("0." + match.Groups[i++].Value);
                var offsetHour = int.Parse(match.Groups[i++].Value);
                var offsetMinute = int.Parse(match.Groups[i++].Value);
                var offset = new TimeSpan(0, Math.Abs(offsetHour), offsetMinute);
                if (offsetHour < 0)
                {
                    offset = offset.Negate();
                }
                return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc)
                    .Add(TimeSpan.FromSeconds(second))
                    .Add(offset)
                    .ToLocalTime();
            }
            else
            {
                return null;
            }
        }

        public void Close()
        {
            if (ssh.IsConnected)
            {
                ssh.Disconnect();
            }
            if (scp.IsConnected)
            {
                scp.Disconnect();
            }
        }
    }

    class ScpSyncClientFactory : ISyncClientFactory
    {
        public ISyncClient Create(string target)
        {
            return new ScpSyncClient(target);
        }
    }
}
