using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConvertNewLine
{
    class MainClass
    {
        private static IEnumerable<string> EnumerateFiles(string directory, IEnumerable<string> extensions)
        {
            foreach (var i in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (i.Split(Path.DirectorySeparatorChar).Any(j => j.StartsWith(".")))
                {
                    continue;
                }

                if (!extensions?.Any(j => i.EndsWith("." + j, StringComparison.OrdinalIgnoreCase)) ?? false)
                {
                    continue;
                }

                yield return i;
            }
        }

        private static IEnumerable<string> EnumerateGitFiles(string directory)
        {
            using (var repository = new Repository(directory))
            {
                foreach (var i in repository.RetrieveStatus().Where(i => i.State == FileStatus.ModifiedInWorkdir))
                {
                    var file = Path.Combine(directory, i.FilePath);
                    if (File.Exists(file))
                    {
                        yield return file;
                    }
                }
            }
            yield break;
        }

        public static void Main(string[] args)
        {
            var argIndex = 0;
            var newlineType = args.Length > 1 ? args[argIndex++] : null;
            var type = (string)null;
            var extensions = new List<string>();
            while (argIndex < args.Length - 1)
            {
                var arg = args[argIndex++];
                if (arg.StartsWith("."))
                {
                    extensions.Add(arg.TrimStart('.'));
                }
                else if (type == null)
                {
                    type = arg;
                }
            }
            var directory = args[argIndex++];

            string newline;
            switch (newlineType)
            {
                case "crlf": newline = "\r\n"; break;
                case "cr": newline = "\r"; break;
                case "lf":
                case null: newline = "\n"; break;
                default: throw new InvalidOperationException("Invalid newline type " + newlineType);
            }

            if (type == null && extensions.Any())
            {
                type = "files";
            }
            else if (Repository.IsValid(directory))
            {
                type = "git";
            }

            var tempFile = Path.GetTempFileName();
            var files = type == "git" ? EnumerateGitFiles(directory) :
                type == "files" ? EnumerateFiles(directory, extensions) :
                null;
            var allowBinary = true;
            if (files == null)
            {
                allowBinary = false;
                files = EnumerateFiles(directory, null);
            }
            foreach (var i in files)
            {
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
                    try
                    {
                        File.Copy(tempFile, i, true);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex);
                    }
                }
            }
        }
    }
}
