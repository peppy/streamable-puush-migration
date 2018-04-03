using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace video_migration
{
    internal class Program
    {
        private const string puush_username = "pe@ppy.sh";
        private const string puush_api_key = "";

        private const string replace_path = "/Users/Dean/Projects/ppy.github.io/_posts/";

        private static readonly Dictionary<string, string> oldNewMapping = new Dictionary<string, string>();

        private static void Main(string[] args)
        {
            Regex urlMatcher = new Regex("//streamable.com/[^\\]\\)\"]*");

            int i = 0;
            foreach (var f in Directory.GetFiles(replace_path, "*.md", SearchOption.AllDirectories))
            {
                Console.WriteLine($"Parsing {f}");

                string fileText = File.ReadAllText(f);
                foreach (Match matches in urlMatcher.Matches(fileText))
                {
                    var streamableUrl = matches.Value;
                    if (streamableUrl.StartsWith("//")) streamableUrl = streamableUrl.Replace("//", "https://");
                    Console.WriteLine($"#{++i}: Performing migration of {streamableUrl}...");

                    if (!oldNewMapping.TryGetValue(streamableUrl, out var puushUrl))
                        puushUrl = migrate(streamableUrl);

                    fileText = fileText.Replace(matches.Value, puushUrl.Replace("https://", "//"));
                    File.WriteAllText(f, fileText);
                }
            }

            Console.WriteLine($"Completed {i} migrations!");
            Console.ReadLine();
        }

        private static string migrate(string streamableUrl)
        {
            var pi = new ProcessStartInfo("/usr/local/bin/youtube-dl", streamableUrl)
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };

            var process = Process.Start(pi);
            Debug.Assert(process != null);

            Console.WriteLine(process.StandardOutput.ReadToEnd());

            process.WaitForExit();

            var output = Directory.GetFiles(Environment.CurrentDirectory, "*.mp4").First();
            Console.WriteLine($"Retrieved {output}");

            HttpContent bytesContent = new ByteArrayContent(File.ReadAllBytes(output));

            Console.WriteLine("Uploading to puush...");

            using (var client = new HttpClient())
            using (var fd = new MultipartFormDataContent())
            {
                fd.Add(new StringContent("poop"), "z");
                fd.Add(new StringContent(puush_username), "e");
                fd.Add(new StringContent(puush_api_key), "k");

                fd.Add(bytesContent, "f", Path.GetFileName(output));

                var response = client.PostAsync("http://puush.me/api/up", fd).Result;

                // ensure the request was a success
                if (!response.IsSuccessStatusCode)
                    Console.WriteLine($"FAILED ({response.StatusCode})");

                var result = response.Content.ReadAsStreamAsync().Result;

                using (var sr = new StreamReader(result))
                {
                    var uploadPath = sr.ReadToEnd().Split(',')[1];
                    Console.WriteLine($"Upload complete! {uploadPath}");

                    oldNewMapping.Add(streamableUrl, uploadPath);

                    File.Delete(output);
                    return uploadPath;
                }
            }
        }
    }
}