using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploads
{
    class Driver
    {
        static void Main(string[] args)
        {
            string pref = "http://localhost:8080/";

            Console.WriteLine("Starting file server on {0}.", pref);
            FileServer server = new FileServer(pref);
            server.Start();
            Console.WriteLine("Server started. Press a key to quit.");
            Console.ReadKey();
            server.Stop();
            Console.WriteLine("Done!");
        }
    }
}
