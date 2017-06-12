using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;

namespace SendTorrentToYatp
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var argIndex = 0;
            var directory = args[argIndex++];
            var url = "http://osmc:9091/transmission/rpc";
            var downloadDirectory = "/media/EXTERNAL/downloads";
            var files = Directory.GetFiles(directory, "*.torrent");
            foreach (var i in files)
            {
                var file = i;
                if (file.StartsWith(directory))
                {
                    file = file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar);
                }
                Console.WriteLine(file);
            }
            Console.ReadLine();

            var client = new Client(url);
            foreach (var i in files)
            {
                var file = i;
                if (file.StartsWith(directory))
                {
                    file = file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar);
                }
                var bytes = File.ReadAllBytes(i);
                var base64 = Convert.ToBase64String(bytes);
                var torrent = new NewTorrent
                {
                    Metainfo = base64,
                    DownloadDirectory = downloadDirectory,
                    Paused = false,
                };
                var result = client.TorrentAdd(torrent);
                if ((result?.ID ?? 0) != 0)
                {
                    var loaded = Path.Combine("torrents", i + ".loaded");
                    if (File.Exists(loaded))
                    {
                        File.Delete(loaded);
                    }
                    if (!Directory.Exists("torrents"))
                    {
                        Directory.CreateDirectory("torrents");
                    }
                    File.Move(i, loaded);
                }
                else
                {
                    Console.Error.WriteLine("Error adding torrent", file);
                }
            }
        }
    }
}
