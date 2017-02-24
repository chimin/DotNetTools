using System;
using System.Diagnostics;

namespace RemoteSync
{
    class SimpleProcess
    {
        public string FileName { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public Action<string> OutputReader { get; set; }

        public int Run()
        {
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FileName,
                    Arguments = Arguments,
                    WorkingDirectory = WorkingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            })
            {
                var outputReader = OutputReader;
                if (outputReader != null)
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputReader(e.Data);
                        }
                    };
                }

                process.Start();
                if (outputReader != null)
                {
                    process.BeginOutputReadLine();
                }

                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
