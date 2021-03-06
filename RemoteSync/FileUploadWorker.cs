using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteSync
{
    class FileUploadWorker : IDisposable
    {
        private ISyncClient syncClient;
        private string sourceDirectory;
        private Func<string, bool> validateFile;
        private Action<string, object[]> log;
        private Action<Exception> logError;
        private LinkedList<string> queues = new LinkedList<string>();
        private Task task;

        public Action Idle { get; set; }

        public FileUploadWorker(ISyncClient syncClient, string sourceDirectory,
                                Func<string, bool> validateFile,
                                Action<string, object[]> log,
                                Action<Exception> logError)
        {
            this.syncClient = syncClient;
            this.sourceDirectory = sourceDirectory;
            this.validateFile = validateFile;
            this.log = log;
            this.logError = logError;
        }

        public string ResolveTargetFile(string sourceFile)
        {
            var targetFile = sourceFile;
            if (targetFile.StartsWith(sourceDirectory))
            {
                targetFile = targetFile.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            if (Path.DirectorySeparatorChar != '/')
            {
                targetFile = targetFile.Replace(Path.DirectorySeparatorChar, '/');
            }
            return targetFile;
        }

        public bool ValidateFile(string sourceFile)
        {
            return validateFile(ResolveTargetFile(sourceFile));
        }

        public bool Add(string sourceFile, bool immediate)
        {
            var targetFile = ResolveTargetFile(sourceFile);
            if (!validateFile(targetFile))
            {
                return false;
            }

            lock (queues)
            {
                if (!queues.Contains(sourceFile))
                {
                    if (immediate)
                    {
                        queues.AddFirst(sourceFile);
                    }
                    else
                    {
                        queues.AddLast(sourceFile);
                    }
                }

                if (task?.IsCompleted ?? true)
                {
                    task = Task.Factory.StartNew(() =>
                    {
                        for (;;)
                        {
                            var thisSourceFile = (string)null;
                            try
                            {
                                lock (queues)
                                {
                                    if (queues.Count > 0)
                                    {
                                        thisSourceFile = queues.First();
                                        queues.Remove(thisSourceFile);
                                    }
                                    else
                                    {
                                        Idle?.Invoke();
                                        task = null;
                                        break;
                                    }
                                }

                                if (File.Exists(thisSourceFile) || Directory.Exists(thisSourceFile))
                                {
                                    Upload(thisSourceFile);
                                }
                            }
                            catch (Exception ex)
                            {
                                logError(ex);
                                syncClient.Close();
                                Thread.Sleep(1000);
                                if (!string.IsNullOrEmpty(thisSourceFile))
                                {
                                    lock (queues)
                                    {
                                        queues.AddLast(thisSourceFile);
                                    }
                                }
                            }
                        }
                    }, TaskCreationOptions.LongRunning);
                }
            }

            return true;
        }

        private void Upload(string sourceFile)
        {
            if (Directory.Exists(sourceFile))
            {
                foreach (var i in Directory.EnumerateFileSystemEntries(sourceFile))
                {
                    Add(i, false);
                }
            }
            else
            {
                int remaining;
                lock (queues)
                {
                    remaining = queues.Count;
                }

                var targetFile = ResolveTargetFile(sourceFile);
                log("Upload file:{0} remaining:{1}", new object[] { targetFile, remaining });
                syncClient.Upload(sourceFile, targetFile);
            }
        }

        public void Wait()
        {
            var task = this.task;
            if (task != null)
            {
                task.Wait();
            }
        }

        public void Dispose()
        {
            Wait();
        }
    }
}
