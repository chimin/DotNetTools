using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            Console.WriteLine("[" + DateTime.Now.ToDateTimeString() + "] " + string.Format(format, args));
        }

        private static void LogError(string format, params object[] args)
        {
            Console.Error.WriteLine("[" + DateTime.Now.ToDateTimeString() + "] " + string.Format(format, args));
        }

        private static void LogError(Exception ex)
        {
            Console.Error.WriteLine("[" + DateTime.Now.ToDateTimeString() + "] " + ex.ToString());
        }

        private static void Sync(ISyncClientFactory syncClientFactory, FileUploadWorker fileUploadWorker,
                                 string source, string target)
        {
            using (var syncClient = syncClientFactory.Create(target))
            {
                foreach (var i in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
                {
                    if (fileUploadWorker.ValidateFile(i))
                    {
                        var targetFile = fileUploadWorker.ResolveTargetFile(i);
                        for (;;)
                        {
                            try
                            {
                                var dirty = false;
                                var sourceFileInfo = new FileInfo(i);
                                var targetFileInfo = syncClient.GetFileInfo(targetFile);
                                if (!targetFileInfo.Exists)
                                {
                                    Log("Sync file:{0} not exists", targetFile);
                                    dirty = true;
                                }
                                else
                                {
                                    if (targetFileInfo?.Size != null && sourceFileInfo.Length != targetFileInfo.Size.Value)
                                    {
                                        Log("Sync file:{0} size:{1} {2}",
                                            targetFile, sourceFileInfo.Length, targetFileInfo.Size.Value);
                                        dirty = true;
                                    }
                                    if (targetFileInfo?.Timestamp != null && sourceFileInfo.LastWriteTime > targetFileInfo.Timestamp.Value)
                                    {
                                        Log("Sync file:{0} timestamp:{1} {2}",
                                            targetFile,
                                            sourceFileInfo.LastWriteTime.ToDateTimeString(),
                                            targetFileInfo.Timestamp.Value.ToDateTimeString());
                                        dirty = true;
                                    }
                                }

                                if (dirty)
                                {
                                    fileUploadWorker.Add(i);
                                }
                                break;
                            }
                            catch (Exception ex)
                            {
                                LogError("Sync file:{0} exception:{1}", targetFile, ex);
                                syncClient.Close();
                                Thread.Sleep(1000);
                            }
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

            var syncIgnoreFile = Path.Combine(source, ".syncignore");
            Func<string, bool> validateFile;
            if (File.Exists(syncIgnoreFile))
            {
                var ignoreFiles = File.ReadAllLines(syncIgnoreFile)
                                      .Where(i => !string.IsNullOrWhiteSpace(i))
                                      .Select(i => i.Trim())
                                      .ToArray();
                validateFile = file => file != ".syncignore" && !ignoreFiles.Any(i => file.StartsWith(i));
            }
            else
            {
                validateFile = file => true;
            }

            var fileUploadWorker = new FileUploadWorker(syncClient, source, validateFile, Log, LogError);
            var syncTask = SyncAsync(syncClientFactory, fileUploadWorker, source, target);
            fileUploadWorker.Idle += () =>
            {
                if (syncTask.IsCompleted)
                {
                    Log("Idle", new object[0]);
                }
                else
                {
                    Log("Still syncing", new object[0]);
                }
            };

            Log("Watch started");
            var fswatchProcess = new SimpleProcess
            {
                FileName = "/usr/local/bin/fswatch",
                Arguments = "--help",
            };
            if (File.Exists(fswatchProcess.FileName) && fswatchProcess.Run() == 0)
            {
                Log("Use fswatch");

                // use fswatch if possible because FileSystemWatcher is not working nice on Mono
                fswatchProcess.Arguments = "-r .";
                fswatchProcess.WorkingDirectory = source;
                fswatchProcess.OutputReader = data =>
                {
                    if (!string.IsNullOrEmpty(data) && (File.Exists(data) || Directory.Exists(data)) &&
                        fileUploadWorker.ValidateFile(data))
                    {
                        Log("Changed file:{0}", fileUploadWorker.ResolveTargetFile(data));
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
                        var file = Path.Combine(fsw.Path, r.Name);
                        if (!r.TimedOut && (File.Exists(file) || Directory.Exists(file)) &&
                            fileUploadWorker.ValidateFile(file))
                        {
                            Log("Changed file:{0} type:{1}", fileUploadWorker.ResolveTargetFile(file), r.ChangeType);
                            fileUploadWorker.Add(file);
                        }
                    }
                }
            }
        }
    }
}
