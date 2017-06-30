using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ConvertStringsToCsv
{
    class Program
    {
        private static string Unescape(string input)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (c == '\\')
                {
                    if (++i < input.Length)
                    {
                        c = input[i];
                        switch (c)
                        {
                            case 'r': sb.Append('\r'); break;
                            case 'n': sb.Append('\n'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append(c); break;
                        }
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        static int Main(string[] args)
        {
            var argIndex = 0;

            if (argIndex >= args.Length)
            {
                Console.Error.WriteLine("Need to specify input file");
                return 1;
            }
            var input = args[argIndex++];

            if (argIndex < args.Length)
            {
                Console.SetOut(File.CreateText(args[argIndex++]));
                Console.OutputEncoding = Encoding.UTF8;
            }

            using (var reader = File.OpenText(input))
            using (var writer = new CsvWriter(Console.Out))
            {
                writer.WriteField("Key");
                writer.WriteField("Value");
                writer.NextRecord();
                for (string line; (line = reader.ReadLine()) != null;)
                {
                    var match = Regex.Match(line, "\"(.*)\" = \"(.*)\";");
                    if (match.Success)
                    {
                        var key = Unescape(match.Groups[1].Value);
                        var value = Unescape(match.Groups[2].Value);
                        writer.WriteField(key);
                        writer.WriteField(value);
                        writer.NextRecord();
                    }
                }
            }

            return 0;
        }
    }
}
