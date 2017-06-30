using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertTable
{
    class CsvTableWriter : ITableWriter
    {
        public string Name { get; } = "csv";

        public void Write(string output, IEnumerable<IList<object>> items)
        {
            var consoleOutputEncoding = Console.OutputEncoding;
            TextWriter writer;
            if (!string.IsNullOrEmpty(output))
            {
                writer = File.CreateText(output);
            }
            else
            {
                writer = Console.Out;
                Console.OutputEncoding = Encoding.UTF8;
            }

            try
            {
                var csv = new CsvWriter(writer);
                foreach (var i in items)
                {
                    foreach (var j in i)
                    {
                        csv.WriteField(j?.ToString());
                    }
                    csv.NextRecord();
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(output))
                {
                    writer.Dispose();
                }
                else
                {
                    Console.OutputEncoding = consoleOutputEncoding;
                }
            }
        }
    }
}
