using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RemoteSync
{
    public static class SystemUtils
    {
        public static void RunWithWatchdog(Func<bool> action, TimeSpan watchdogDuration)
        {
            var sync = new object();
            var heartbeat = DateTime.Now;
            RunWithWatchdog(() =>
            {
                while (action())
                {
                    lock (sync)
                    {
                        heartbeat = DateTime.Now;
                    }
                }
            }, watchdogDuration, ref heartbeat, sync);
        }

        public static void RunWithWatchdog(Action action, TimeSpan watchdogDuration, ref DateTime heartbeat, object sync)
        {
            Exception exception = null;
            var thread = new Thread(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            })
            { IsBackground = true };
            thread.Start();

            for (;;)
            {
                DateTime next;
                lock (sync)
                {
                    next = heartbeat + watchdogDuration;
                }

                var now = DateTime.Now;
                if (now > next)
                {
                    throw new TimeoutException();
                }

                if (thread.Join(next - now))
                {
                    break;
                }
            }
            if (exception != null)
            {
                throw new ArgumentException("Run error", exception);
            }
        }
    }
}
