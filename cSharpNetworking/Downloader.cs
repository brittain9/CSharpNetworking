using System;
using System.IO;
using System.Text;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// HttpClient establishes a connection over TCP to get a response from any server
// serves web pages (or responds to HTTP requests). Simple, but require async code
// async, await, Task

namespace HttpClient
{
    class Downloader
    {
        public static string urlToDownload = "https://tbrittain.com/";
        public static string fileName = "index.html";

        public static async Task DownloadWebPage()
        {
            Console.WriteLine("Starting download...");
            
            // Setup HttpClient
            using (System.Net.Http.HttpClient httpClient = new System.Net.Http.HttpClient())
            {
                // Get the webpage asynchronously
                HttpResponseMessage resp = await httpClient.GetAsync(urlToDownload);

                // If we get a 200 response, then save it
                if (resp.IsSuccessStatusCode)
                {
                    Console.WriteLine("Got it...");

                    // Get the data
                    byte[] data = await resp.Content.ReadAsByteArrayAsync();

                    // Save to file
                    FileStream fStream = File.Create(fileName);
                    await fStream.WriteAsync(data, 0, data.Length);
                    fStream.Close();

                    Console.WriteLine("Done!");
                }

            }

        }

        public static void Main(string[] args)
        {
            Task dlTask = DownloadWebPage();

            Console.WriteLine("Holding for at least 5 seconds...");
            Thread.Sleep(TimeSpan.FromSeconds(5));

            dlTask.GetAwaiter().GetResult();
        }
    }

}

