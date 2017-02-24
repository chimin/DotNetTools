using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteSync
{
    class MainClass
    {
        private static ISyncClientFactory GetSyncClientFactory(string type)
        {
            switch (type)
            {
                case "scp": return new ScpSyncClientFactory();
                case "ftp": return new FtpSyncClientFactory();
                default: throw new ArgumentException("Unknown sync client " + type);
            }
        }

        public static void Main(string[] args)
        {
            var argIndex = 0;
            var source = args[argIndex++];
            var targetType = args[argIndex++];
            var target = args[argIndex++];

            Console.WriteLine("Source: {0}", source);
            Console.WriteLine("Target: {0} {1}", targetType, target);

            var syncClientFactory = GetSyncClientFactory(targetType);

            using (var syncClient = syncClientFactory.Create(target))
            {
                syncClient.Test();
            }

            var uploadQueue = new BlockingCollection<string>();
            var uploadException = (Exception)null;
            var uploadThread = new Thread(_ =>
            {
                try
                {
                    using (var syncClient = syncClientFactory.Create(target))
                    {
                        while (!uploadQueue.IsCompleted)
                        {
                            var sourceFile = uploadQueue.Take();
                            var targetFile = sourceFile;
                            if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFile = targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                            }

                            Console.WriteLine("[{0}] Upload file:{1} to:{2}", DateTime.Now, sourceFile, targetFile);
                            try
                            {
                                syncClient.Upload(sourceFile, targetFile);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine(ex);
                                syncClient.Close();
                                Thread.Sleep(1000);
                                lock (uploadQueue)
                                {
                                    if (!uploadQueue.Contains(sourceFile))
                                    {
                                        uploadQueue.Add(sourceFile);
                                    }
                                }
                            }

                            if (uploadQueue.Count == 0)
                            {
                                Console.WriteLine("[{0}] Waiting", DateTime.Now);
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

                using (var syncClient = syncClientFactory.Create(target))
                {
                    foreach (var i in Directory.EnumerateFiles(source))
                    {
                        if (i.Split(Path.DirectorySeparatorChar).Any(j => j.StartsWith(".", StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        var targetFile = i;
                        if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                        {
                            targetFile = targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                        }

                        for (;;)
                        {
                            try
                            {
                                var fileSize = syncClient.GetFileSize(targetFile);
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

                                break;
                            }
                            catch (Renci.SshNet.Common.SshException ex)
                            {
                                Console.Error.WriteLine(ex);
                                syncClient.Close();
                                Thread.Sleep(1000);
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
            var fswatchProcess = new SimpleProcess
            {
                FileName = "/usr/local/bin/fswatch",
                Arguments = "--help",
            };
            if (fswatchProcess.Run() == 0)
            {
                Console.WriteLine("Use fswatch");

                // use fswatch if possible because FileSystemWatcher is not working nice on Mono
                fswatchProcess.Arguments = "-r .";
                fswatchProcess.WorkingDirectory = source;
                fswatchProcess.OutputReader = data =>
                {
                    if (!string.IsNullOrEmpty(data) && File.Exists(data) &&
                        !data.Split(Path.DirectorySeparatorChar).Any(i => i.StartsWith(".", StringComparison.Ordinal)))
                    {
                        lock (uploadQueue)
                        {
                            if (!uploadQueue.Contains(data))
                            {
                                uploadQueue.Add(data);
                            }
                        }
                    }
                };
                Task<int> fswatchTask = Task.Run(fswatchProcess.Run);
                for (;;)
                {
                    {
                        var e = uploadException;
                        if (e != null)
                        {
                            throw e;
                        }
                    }


                    if (fswatchTask.Wait(1000))
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
