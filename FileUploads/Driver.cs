using System;
using System.Threading;

namespace FileUploads
{
    class Driver
    {
        static void Main(string[] args)
        {
            string pref = "http://localhost:8080/";
            int numMaxParallelRequests = 16;
            string fileUploadDestination = @"C:\uploads";

            ThreadPool.SetMinThreads(numMaxParallelRequests, 0);

            Console.WriteLine("Starting file server on {0}.", pref);
            FileServer server = new FileServer(pref, numMaxParallelRequests, fileUploadDestination);
            server.Start();
            Console.WriteLine("Server started. Press a key to quit.");
            Console.ReadKey();
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
