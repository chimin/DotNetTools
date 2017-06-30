using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertTable
{
    class LaravelLangWriter : ITableWriter
    {
        private static Dictionary<char, string> phpStringSpecialChars = new Dictionary<char, string> {
            { '\\', @"\\" },
            { '\n', @"\n" },
            { '\r', @"\r" },
            { '\t', @"\t" },
            { '\'', @"\'" },
            { '"', @"\""" },
            { '$', @"\$" },
        };

        private static string EncodePhpString(string input)
        {
            decimal number;
            if (decimal.TryParse(input, out number))
            {
                return number.ToString();
            }

            var sb = new StringBuilder();
            var doubleQuote = false;
            foreach (var i in input)
            {
                string escaped;
                if (phpStringSpecialChars.TryGetValue(i, out escaped))
                {
                    doubleQuote = true;
                    sb.Append(escaped);
                }
                else
                {
                    sb.Append(i);
                }
            }
            if (doubleQuote)
            {
                sb.Insert(0, '"');
                sb.Append('"');
            }
            else
            {
                sb.Insert(0, '\'');
                sb.Append('\'');
            }
            return sb.ToString();
        }

        public string Name { get; } = "laravel-lang";

        public void Write(string output, IEnumerable<IList<object>> items)
        {
            var outputItems = output.Split(new[] { '@' }, 2);
            var directory = outputItems[0];
            var name = outputItems[1];

            var e = items.GetEnumerator();
            if (e.MoveNext())
            {
                var writers = new List<TextWriter>();
                try
                {
                    var headers = e.Current;
                    foreach (var i in headers.Skip(1))
                    {
                        var file = Path.Combine(directory, i.ToString(), name + ".php");
                        var writer = File.CreateText(file);
                        writer.NewLine = "\n";
                        writer.WriteLine("<?php");
                        writer.WriteLine();
                        writer.WriteLine("return [");
                        writers.Add(writer);
                    }

                    var dict = new Dictionary<string, IList<object>>();
                    while (e.MoveNext())
                    {
                        dict[e.Current[0].ToString()] = e.Current;
                    }
                    while (dict.Any())
                    {
                        var key = dict.Keys.First();
                        var seperator = key.IndexOf('.');
                        if (seperator >= 0)
                        {
                            var baseKey = key.Substring(0, seperator);
                            var pattern = baseKey + ".";
                            var subKeys = dict.Keys.Where(i => i.StartsWith(pattern)).ToArray();
                            for (var i = 1; i < headers.Count; i++)
                            {
                                var writer = writers[i - 1];
                                writer.Write("\t");
                                writer.Write(EncodePhpString(baseKey));
                                writer.WriteLine(" => [");
                                foreach (var j in subKeys)
                                {
                                    var value = dict[j].ElementAtOrDefault(i)?.ToString();
                                    if (!string.IsNullOrEmpty(value))
                                    {
                                        var subKey = j.Substring(j.IndexOf('.') + 1);
                                        writer.Write("\t\t");
                                        writer.Write(EncodePhpString(subKey));
                                        writer.Write(" => ");
                                        writer.Write(EncodePhpString(value));
                                        writer.WriteLine(",");
                                    }
                                }
                                writer.WriteLine("\t],");
                            }
                            foreach (var i in subKeys)
                            {
                                dict.Remove(i);
                            }
                        }
                        else
                        {
                            for (var i = 1; i < headers.Count; i++)
                            {
                                var value = dict[key].ElementAtOrDefault(i)?.ToString();
                                if (!string.IsNullOrEmpty(value))
                                {
                                    var writer = writers[i - 1];
                                    writer.Write("\t");
                                    writer.Write(EncodePhpString(key));
                                    writer.Write(" => ");
                                    writer.Write(EncodePhpString(value));
                                    writer.WriteLine(",");
                                }
                            }
                            dict.Remove(key);
                        }
                    }
                }
                finally
                {
                    foreach (var i in writers)
                    {
                        i.WriteLine("];");
                        i.Dispose();
                    }
                }
            }
        }
    }
}
