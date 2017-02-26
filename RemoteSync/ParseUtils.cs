using System;
namespace RemoteSync
{
    static class ParseUtils
    {
        public static string ToDateTimeString(this DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }
}
