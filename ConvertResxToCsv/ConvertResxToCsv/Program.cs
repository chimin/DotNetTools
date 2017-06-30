using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace ConvertResxToCsv
{
    class Program
    {
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

            using (var reader = XmlReader.Create(File.OpenText(input)))
            using (var writer = new CsvWriter(Console.Out))
            {
                writer.WriteField("Key");
                writer.WriteField("Value");
                writer.NextRecord();

                reader.ReadToFollowing("root");
                for (reader.ReadToFollowing("data"); reader.ReadToNextSibling("data");)
                {
                    var key = reader.GetAttribute("name");
                    using (var subtree = reader.ReadSubtree())
                    {
                        subtree.ReadToDescendant("value");
                        var value = subtree.ReadElementContentAsString();
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
