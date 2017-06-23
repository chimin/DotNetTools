using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace RemoteSync
{
    public class FswatchFileSystemWatcher : IFileSystemWatcher
    {
        private Process process;
        private string directory;
        private BlockingCollection<string> changedFiles = new BlockingCollection<string>();

        public static FswatchFileSystemWatcher Create(string directory)
        {
            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/local/bin/fswatch",
                        Arguments = "--help",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
						CreateNoWindow = true,
					}
                })
                {
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        return new FswatchFileSystemWatcher(directory);
                    }
                }
            }
            catch { }
            return null;
        }

        public FswatchFileSystemWatcher(string directory)
        {
            this.process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/local/bin/fswatch",
                    Arguments = "-r .",
                    WorkingDirectory = directory,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            this.directory = directory;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    lock (changedFiles)
                    {
                        if (!changedFiles.Contains(e.Data))
                        {
                            changedFiles.Add(e.Data);
                        }
                    }
                }
            };
            process.Start();
            process.BeginOutputReadLine();
        }

        public void Dispose()
        {
            process.Kill();
            process.Dispose();
            changedFiles.CompleteAdding();
        }

        public string WaitForChanged()
        {
            return changedFiles.Take();
        }
    }
}
