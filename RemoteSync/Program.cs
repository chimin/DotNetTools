using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace RemoteSync
{
    class MainClass
    {
        private static Renci.SshNet.ScpClient CreateScpClient(string target, out string targetDirectory)
        {
            var pattern = new Regex("^(.*)@(.*):(.*)");
            var match = pattern.Match(target);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid target " + target);
            }

            var user = match.Groups[1].Value;
            var host = match.Groups[2].Value;
            targetDirectory = match.Groups[3].Value;

            var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".ssh");
            var keyFiles = new List<Renci.SshNet.PrivateKeyFile>();
            foreach (var i in Directory.EnumerateFiles(keyDirectory))
            {
                if (File.Exists(i + ".pub"))
                {
                    keyFiles.Add(new Renci.SshNet.PrivateKeyFile(i));
                }
            }

            return new Renci.SshNet.ScpClient(host, user, keyFiles.ToArray());
        }

        private static Renci.SshNet.SshClient CreateSshClient(string target, out string targetDirectory)
        {
            var pattern = new Regex("^(.*)@(.*):(.*)");
            var match = pattern.Match(target);
            if (!match.Success)
            {
                throw new ArgumentException("Invalid target " + target);
            }

            var user = match.Groups[1].Value;
            var host = match.Groups[2].Value;
            targetDirectory = match.Groups[3].Value;

            var keyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".ssh");
            var keyFiles = new List<Renci.SshNet.PrivateKeyFile>();
            foreach (var i in Directory.EnumerateFiles(keyDirectory))
            {
                if (File.Exists(i + ".pub"))
                {
                    keyFiles.Add(new Renci.SshNet.PrivateKeyFile(i));
                }
            }

            return new Renci.SshNet.SshClient(host, user, keyFiles.ToArray());
        }

        public static void Main(string[] args)
        {
            var argIndex = 0;
            var source = args[argIndex++];
            var target = args[argIndex++];

            Console.WriteLine("Source: {0}", source);
            Console.WriteLine("Target: {0}", target);

            // somehow SSH is not working without this...
            {
                string targetDirectory;
                using (var c = CreateSshClient(target, out targetDirectory))
                {
                    c.Connect();
                }
            }

            var uploadQueue = new BlockingCollection<string>();
            var uploadException = (Exception)null;
            var uploadThread = new Thread(_ =>
            {
                try
                {
                    string targetDirectory;
                    using (var scp = CreateScpClient(target, out targetDirectory))
                    {
                        scp.Connect();
                        targetDirectory = targetDirectory.TrimEnd('/');

                        var ignoreErrorRegex = new Regex("^scp: .*: set times: Operation not permitted$");
                        while (!uploadQueue.IsCompleted)
                        {
                            var sourceFile = uploadQueue.Take();
                            var targetFile = sourceFile;
                            if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFile = targetDirectory + "/" +
                                    targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                            }

                            Console.WriteLine("Upload file:{0} to:{1}", sourceFile, targetFile);
                            try
                            {
                                scp.Upload(new FileInfo(sourceFile), targetFile);
                            }
                            catch (Renci.SshNet.Common.ScpException ex)
                            {
                                if (!ignoreErrorRegex.IsMatch(ex.Message))
                                {
                                    throw;
                                }
                            }

                            if (uploadQueue.Count == 0)
                            {
                                Console.WriteLine("Waiting");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    uploadException = ex;
                }
            })
            {
                Name = "Upload",
                IsBackground = true,
            };
            uploadThread.Start();

            var syncThread = new Thread(_ =>
            {
                Console.WriteLine("Sync started");
               
                string targetDirectory;
                using (var ssh = CreateSshClient(target, out targetDirectory))
                {
                    ssh.Connect();

                    foreach (var i in Directory.EnumerateFiles(source))
                    {
                        if (i.Split(Path.DirectorySeparatorChar).Any(j => j.StartsWith(".", StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        var targetFile = i;
                        if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                        {
                            targetFile = targetDirectory + "/" +
                                targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                        }

                        var info = ssh.RunCommand("ls -l \"" + targetFile + "\"").Result.Trim();
                        var fileSize = !string.IsNullOrEmpty(info) ? long.Parse(info.Split(' ')[4]) : 0;
                        if (fileSize != new FileInfo(i).Length)
                        {
                            lock (uploadQueue)
                            {
                                if (!uploadQueue.Contains(i))
                                {
                                    uploadQueue.Add(i);
                                }
                            }
                        }
                    }
                }

                Console.WriteLine("Sync done");
            })
            {
                Name = "Sync",
                IsBackground = true,
            };
            syncThread.Start();

            Console.WriteLine("Watch started");
            var fswatchProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/local/bin/fswatch",
                    Arguments = "--help",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                },
            };
            fswatchProcess.Start();
            fswatchProcess.WaitForExit();
            if (fswatchProcess.ExitCode == 0)
            {
                Console.WriteLine("Use fswatch");

                // use fswatch if possible because FileSystemWatcher is not working nice on Mono
                fswatchProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fswatchProcess.StartInfo.FileName,
                        Arguments = "-r .",
                        WorkingDirectory = source,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    },
                };
                fswatchProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data) && File.Exists(e.Data) &&
                        !e.Data.Split(Path.DirectorySeparatorChar).Any(i => i.StartsWith(".", StringComparison.Ordinal)))
                    {
                        lock (uploadQueue)
                        {
                            if (!uploadQueue.Contains(e.Data))
                            {
                                uploadQueue.Add(e.Data);
                            }
                        }
                    }
                };
                fswatchProcess.Start();
                fswatchProcess.BeginOutputReadLine();
                for (;;)
                {
                    {
                        var e = uploadException;
                        if (e != null)
                        {
                            throw e;
                        }
                    }


                    if (fswatchProcess.WaitForExit(1000))
                    {
                        throw new InvalidOperationException("Fswatch terminated unexpectedly");
                    }
                }
            }
            else
            {
                Console.WriteLine("Use FileSystemWatcher");
                
                using (var fsw = new FileSystemWatcher
                {
                    Path = source,
                    IncludeSubdirectories = true,
                })
                {
                    for (;;)
                    {
                        {
                            var e = uploadException;
                            if (e != null)
                            {
                                throw e;
                            }
                        }

                        var r = fsw.WaitForChanged(WatcherChangeTypes.All, 1000);
                        if (!r.TimedOut && File.Exists(r.Name))
                        {
                            if (r.Name.Split(Path.DirectorySeparatorChar).Any(i => i.StartsWith(".", StringComparison.Ordinal)))
                            {
                                continue;
                            }

                            lock (uploadQueue)
                            {
                                if (!uploadQueue.Contains(r.Name))
                                {
                                    uploadQueue.Add(r.Name);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
