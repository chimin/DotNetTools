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
        private string user;
        private string host;
        private string directory;

        public ScpSyncClient(string target)
        {
            var pattern = new Regex("^(.*)@(.*):(.*)$");
            var match = pattern.Match(target);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid target " + target);
            }

            user = match.Groups[1].Value;
            host = match.Groups[2].Value;
            directory = match.Groups[3].Value.TrimEnd('/');
        }

        public void Dispose()
        {
            if (ssh != null)
            {
                ssh.Dispose();
            }
            if (scp != null)
            {
                scp.Dispose();
            }
            ssh = null;
            scp = null;
        }

        private static Renci.SshNet.PrivateKeyFile[]GetKeyFiles()
        {
            var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".ssh");
            var keyFilePaths = new List<string>();
            foreach (var i in Directory.EnumerateFiles(keyDirectory))
            {
                if (File.Exists(i + ".pub"))
                {
                    keyFilePaths.Add(i);
                }
            }
            return keyFilePaths.Select(i => new Renci.SshNet.PrivateKeyFile(i)).ToArray();
        }

        private Renci.SshNet.SshClient GetSshClient()
        {
            if (ssh == null)
            {
                ssh = new Renci.SshNet.SshClient(host, user, GetKeyFiles());
            }
            if (!ssh.IsConnected)
            {
                ssh.Connect();
            }
            return ssh;
        }

        private Renci.SshNet.ScpClient GetScpClient()
        {
            if (scp == null)
            {
                scp = new Renci.SshNet.ScpClient(host, user, GetKeyFiles());
            }
            if (!scp.IsConnected)
            {
                scp.Connect();
            }
            return scp;
        }

        public void Test()
        {
            GetSshClient();
        }

        public void Upload(string sourceFile, string targetFile)
        {
            if (!targetFile.StartsWith("/"))
            {
                targetFile = directory + "/" + targetFile;
            }

            var triedDelete = false;
            var triedMkdir = false;

        PerformAction:
            var scp = GetScpClient();
            try
            {
                scp.Upload(new FileInfo(sourceFile), targetFile);
            }
            catch (Renci.SshNet.Common.ScpException ex)
            {
                if (!triedDelete && Regex.IsMatch(ex.Message,
                                                  "Permission denied$"))
                {
                    var ssh = GetSshClient();
                    ssh.RunCommand("rm -f \"" + targetFile + "\"");
                    triedDelete = true;
                    goto PerformAction;
                }
                else if (!triedMkdir && Regex.IsMatch(ex.Message,
                                                      "^scp: No such file or directory"))
                {
                    var ssh = GetSshClient();
                    var separator = targetFile.LastIndexOf('/');
                    if (separator >= 0)
                    {
                        var targetDirectory = targetFile.Substring(0, separator);
                        ssh.RunCommand("mkdir -p \"" + targetDirectory + "\"");
                        triedMkdir = true;
                        goto PerformAction;

                    }
                    else
                    {
                        throw;
                    }
                }
                else if (!Regex.IsMatch(ex.Message,
                  "^scp: .*: set times: Operation not permitted$"))
                {
                    throw;
                }
            }
        }

        private string Stat(string targetFile)
        {
            try
            {
                if (!targetFile.StartsWith("/"))
                {
                    targetFile = directory + "/" + targetFile;
                }

                var ssh = GetSshClient();
                return ssh.RunCommand("stat \"" + targetFile + "\"").Result;
            }
            catch (Renci.SshNet.Common.SshException ex)
            {
                if (Regex.IsMatch(ex.Message,
                                  "^stat: .*: No such file or directory$"))
                {
                    return null;
                }
                else
                {
                    throw;
                }
            }
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
                var offset = new TimeSpan(Math.Abs(offsetHour), offsetMinute, 0);
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
            ssh = null;
            scp = null;
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
