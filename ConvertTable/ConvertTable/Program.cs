using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConvertTable
{
    class Program
    {
        private static Dictionary<string, ITableReader> readers = typeof(Program).Assembly.GetTypes()
            .Where(i => typeof(ITableReader).IsAssignableFrom(i))
            .Select(i => (ITableReader)i.GetConstructor(Type.EmptyTypes)?.Invoke(null))
            .Where(i => i != null)
            .ToDictionary(i => i.Name, i => i);
        private static Dictionary<string, ITableWriter> writers = typeof(Program).Assembly.GetTypes()
            .Where(i => typeof(ITableWriter).IsAssignableFrom(i))
            .Select(i => (ITableWriter)i.GetConstructor(Type.EmptyTypes)?.Invoke(null))
            .Where(i => i != null)
            .ToDictionary(i => i.Name, i => i);

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("ConvertTable <reader> <input> <writer> <output>");
                Console.WriteLine("Readers: " + string.Join(" ", readers.Keys));
                Console.WriteLine("Writers: " + string.Join(" ", writers.Keys));
                return;
            }

            var argIndex = 0;
            var reader = readers[args[argIndex++]];
            var input = args[argIndex++];
            var writer = writers[args[argIndex++]];
            var output = argIndex < args.Length ? args[argIndex++] : null;
            writer.Write(output, reader.Read(input));
        }
    }
}
