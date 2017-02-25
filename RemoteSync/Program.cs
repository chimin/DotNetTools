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

        private static void Log(string format, params object[] args)
        {
            Console.WriteLine("[" + DateTime.Now.ToString() + "] " + string.Format(format, args));
        }

        private static void LogError(Exception ex)
        {
            Console.WriteLine(ex);
        }

        private static bool ShouldSync(string file)
        {
            return (File.Exists(file) || Directory.Exists(file)) &&
                !file.Split(Path.DirectorySeparatorChar).Any(i => i.StartsWith("."));
        }

        private static void Sync(ISyncClientFactory syncClientFactory, FileUploadWorker fileUploadWorker,
                                 string source, string target)
        {
            using (var syncClient = syncClientFactory.Create(target))
            {
                foreach (var i in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Where(ShouldSync))
                {
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
                                Log("Sync file:{0}", targetFile);
                                fileUploadWorker.Add(i);
                            }

                            break;
                        }
                        catch (Exception ex)
                        {
                            LogError(ex);
                            syncClient.Close();
                            Thread.Sleep(1000);
                        }
                    }
                }
            }
        }

        private static Task SyncAsync(ISyncClientFactory syncClientFactory, FileUploadWorker fileUploadWorker,
                                      string source, string target)
        {
            return Task.Factory.StartNew(() =>
            {
                Log("Sync started");
                Sync(syncClientFactory, fileUploadWorker, source, target);
                Log("Sync done");
            }, TaskCreationOptions.LongRunning);
        }

        public static void Main(string[] args)
        {
            var argIndex = 0;
            var source = args[argIndex++];
            var targetType = args[argIndex++];
            var target = args[argIndex++];

            Log("Source: {0}", source);
            Log("Target: {0} {1}", targetType, target);
            Log("Starting");

            var syncClientFactory = GetSyncClientFactory(targetType);
            var syncClient = syncClientFactory.Create(target);
            syncClient.Test();

            var fileUploadWorker = new FileUploadWorker(syncClient, source, Log, LogError);
            SyncAsync(syncClientFactory, fileUploadWorker, source, target);

            Log("Watch started");
            var fswatchProcess = new SimpleProcess
            {
                FileName = "/usr/local/bin/fswatch",
                Arguments = "--help",
            };
            if (fswatchProcess.Run() == 0)
            {
                Log("Use fswatch");

                // use fswatch if possible because FileSystemWatcher is not working nice on Mono
                fswatchProcess.Arguments = "-r .";
                fswatchProcess.WorkingDirectory = source;
                fswatchProcess.OutputReader = data =>
                {
                    if (!string.IsNullOrEmpty(data) && ShouldSync(data))
                    {
                        var targetFile = data;
                        if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                        {
                            targetFile = targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                        }

                        fileUploadWorker.Add(data);
                    }
                };

                var exitCode = fswatchProcess.Run();
                throw new InvalidOperationException("Fswatch terminated unexpectedly code:" + exitCode.ToString());
            }
            else
            {
                Log("Use FileSystemWatcher");
                
                using (var fsw = new FileSystemWatcher
                {
                    Path = source,
                    IncludeSubdirectories = true,
                })
                {
                    for (;;)
                    {
                        var r = fsw.WaitForChanged(WatcherChangeTypes.All);
                        if (!r.TimedOut && ShouldSync(r.Name))
                        {
                            var targetFile = r.Name;
                            if (targetFile.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                            {
                                targetFile = targetFile.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar);
                            }

                            fileUploadWorker.Add(r.Name);
                        }
                    }
                }
            }
        }
    }
}
