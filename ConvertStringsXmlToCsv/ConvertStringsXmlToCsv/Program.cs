using CsvHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ConvertStringsXmlToCsv
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

                reader.ReadToFollowing("resources");
                while (reader.Read())
                {
                    if (reader.IsStartElement())
                    {
                        using (var subtree = reader.ReadSubtree())
                        {
                            if (subtree.Read())
                            {
                                if (subtree.IsStartElement("string"))
                                {
                                    var key = subtree.GetAttribute("name");
                                    var value = subtree.ReadElementContentAsString();
                                    writer.WriteField(key);
                                    writer.WriteField(value);
                                    writer.NextRecord();
                                }
                                else if (subtree.IsStartElement("string-array"))
                                {
                                    var baseKey = subtree.GetAttribute("name");
                                    var index = 0;
                                    for (subtree.ReadToFollowing("item"); subtree.ReadToNextSibling("item");)
                                    {
                                        var key = baseKey + "." + (index++).ToString();
                                        var value = subtree.ReadElementContentAsString();
                                        writer.WriteField(key);
                                        writer.WriteField(value);
                                        writer.NextRecord();
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return 0;
        }
    }
}
