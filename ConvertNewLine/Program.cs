using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConvertNewLine
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var argIndex = 0;
            var newlineType = args[argIndex++];
            var extensions = new List<string>();
            while (argIndex < args.Length - 1)
            {
                extensions.Add(args[argIndex++].TrimStart('.'));
            }
            var directory = args[argIndex++];

            string newline;
            switch (newlineType)
            {
                case "crlf": newline = "\r\n"; break;
                case "cr": newline = "\r"; break;
                case "lf": newline = "\n"; break;
                default: throw new InvalidOperationException("Invalid newline type " + newlineType);
            }

            var tempFile = Path.GetTempFileName();
            foreach (var i in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (i.Split(Path.DirectorySeparatorChar).Any(j => j.StartsWith(".")))
                {
                    continue;
                }

                var allowBinary = extensions.Count > 0;
                if (extensions.Count > 0)
                {
                    if (!extensions.Any(j => i.EndsWith("." + j, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                }

                var valid = true;
                using (var writer = File.CreateText(tempFile))
                using (var reader = File.OpenText(i))
                {
                    writer.NewLine = newline;
                    for (int c; (c = reader.Read()) != -1;)
                    {
                        if (c == '\r')
                        {
                            if (reader.Peek() == '\n')
                            {
                                reader.Read();
                            }
                            writer.WriteLine();
                        }
                        else if (c == '\n')
                        {
                            writer.WriteLine();
                        }
                        else if (c >= 128 && !allowBinary)
                        {
                            valid = false;
                            break;
                        }
                        else 
                        {
                            writer.Write((char)c);
                        }
                    }
                }

                if (valid && new FileInfo(tempFile).Length != new FileInfo(i).Length)
                {
                    var file = i;
                    if (file.StartsWith(directory))
                    {
                        file = file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar);
                    }
                    Console.WriteLine("Updated file:{0}", file);
                    File.Copy(tempFile, i, true);
                }
            }
        }
    }
}
