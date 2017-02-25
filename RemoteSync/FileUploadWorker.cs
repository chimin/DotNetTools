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
        private Action<string, object[]> log;
        private Action<Exception> logError;
        private HashSet<string> queues = new HashSet<string>();
        private Task task;

        public FileUploadWorker(ISyncClient syncClient, string sourceDirectory,
                                Action<string, object[]> log,
                                Action<Exception> logError)
        {
            this.syncClient = syncClient;
            this.sourceDirectory = sourceDirectory;
            this.log = log;
            this.logError = logError;
        }

        public bool Add(string sourceFile)
        {
            if (sourceFile.Split(Path.DirectorySeparatorChar).Any(i => i.StartsWith(".")))
            {
                return false;
            }

            lock (queues)
            {
                queues.Add(sourceFile);

                if (task == null || task.IsCompleted)
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
                                        log("Waiting", new object[0]);
                                        task = null;
                                        break;
                                    }
                                }
                                Upload(thisSourceFile);
                            }
                            catch (Exception ex)
                            {
                                logError(ex);
                                syncClient.Close();
                                Thread.Sleep(1000);
                                if (!string.IsNullOrEmpty(thisSourceFile))
                                {
                                    Add(thisSourceFile);
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
                foreach (var i in Directory.EnumerateFiles(sourceFile))
                {
                    Add(i);
                }
            }
            else
            {
                var targetFile = sourceFile;
                if (targetFile.StartsWith(sourceDirectory))
                {
                    targetFile = targetFile.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
                }

                log("Upload file:{0}", new[] { targetFile });
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
