using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelUploader
{
    class Program
    {
        static void Main(string[] args)
        {
            int start = Environment.TickCount;
            int maxParallelUploads = 50;
            string urlForUploads = "http://localhost:8080";
            string dirToUpload = Path.Combine(".", "default-to-upload");
            if (args.Length > 0)
            {
                dirToUpload = args[0];
            }

            ParallelOptions opts = new ParallelOptions();
            opts.MaxDegreeOfParallelism = maxParallelUploads;

            Parallel.ForEach(Directory.EnumerateFiles(dirToUpload, "*", SearchOption.AllDirectories), opts, (filepath) =>
            {
                Console.WriteLine("Uploading {0} on thread {1}...", filepath, Thread.CurrentThread.ManagedThreadId);
                WebClient webClient = new WebClient();
                int sleepPeriodMs = 1000;
                bool retry = true;
                bool success = false;

                while (retry)
                {
                    retry = false;
                    try
                    {
                        webClient.UploadFile(urlForUploads, filepath);
                        success = true;
                    }
                    catch (WebException e)
                    {
                        var r = (HttpWebResponse)e.Response;
                        if (r != null && r.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            // We are overloading the server. Wait some time and try again.
                            Console.WriteLine("Server is overloaded. Retrying in {0}ms...", sleepPeriodMs);
                            Thread.Sleep(sleepPeriodMs);
                            sleepPeriodMs *= 2;
                            retry = true;
                        }
                        else
                        {
                            Console.WriteLine("Failed to upload file {0}. Error was: \n{1}.\nMoving on to next file.", filepath, e.ToString());
                        }
                    }
                    catch
                    {
                        Console.WriteLine("Unexpected error! Failed to upload file {0}. Moving on to next file.", filepath);
                    }
                }

                if (success)
                {
                    // The file was successfully uploaded to the server - delete it!
                    File.Delete(filepath);
                }
            });
            Console.WriteLine("Took {0} ticks to upload files.", Environment.TickCount - start);
        }
    }
}
