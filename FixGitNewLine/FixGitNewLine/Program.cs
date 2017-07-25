using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FixGitNewLine
{
    class Program
    {
        static void Main(string[] args)
        {
            var argIndex = 0;
            var directory = args[argIndex++];
            var extensions = new List<string>();
            while (argIndex < args.Length)
            {
                extensions.Add("." + args[argIndex++].Trim('.'));
            }
            if (!extensions.Any())
            {
                extensions = null;
            }

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var repository = new Repository(directory))
                {
                    foreach (var i in repository.RetrieveStatus().Where(i => i.State == FileStatus.ModifiedInWorkdir))
                    {
                        var file = Path.Combine(directory, i.FilePath);
                        if (extensions?.Any(j => file.EndsWith(j, StringComparison.OrdinalIgnoreCase)) ?? true)
                        {
                            using (var reader = File.OpenText(file))
                            using (var writer = File.CreateText(tempFile))
                            {
                                writer.NewLine = "\n";
                                for (string line; (line = reader.ReadLine()) != null;)
                                {
                                    writer.WriteLine(line);
                                }
                            }
                            if (new FileInfo(file).Length != new FileInfo(tempFile).Length)
                            {
                                File.Copy(tempFile, file, true);
                                Console.WriteLine(i.FilePath);
                            }
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
