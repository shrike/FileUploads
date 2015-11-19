using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace UTFileServer
{
    [TestClass]
    public class UnitTest1
    {
        static string pref = "http://localhost:8080/";
        static FileUploads.FileServer server;
        const int numMaxParallelRequests = 16;

        [ClassInitialize]
        public static void Initialize(TestContext ctx)
        {
            // We need at least numMaxParallelRequests to process that many
            // incoming requests and we need numMaxParallelRequests more threads
            // to be able to issue that many requests in parallel.
            ThreadPool.SetMinThreads(numMaxParallelRequests*2, 0);

            server = new FileUploads.FileServer(pref, numMaxParallelRequests);
            server.Start();
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            server.Stop();
        }

        [TestMethod]
        public void TestSingleRequest()
        {
            WebResponse res = null;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pref);
            req.Timeout = 2000;
            res = req.GetResponse();
        }

        [TestMethod]
        public void TestConsecutiveRequest()
        {
            TestSingleRequest();
            TestSingleRequest();
            TestSingleRequest();
            TestSingleRequest();
        }

        [TestMethod]
        public void TestParallelRequests()
        {
            TaskAwaiter[] waiters = new TaskAwaiter[numMaxParallelRequests];

            for(int i = 0; i < numMaxParallelRequests; i++)
            {
                waiters[i] = Task.Run(() =>
                {
                    TestSingleRequest();
                }).GetAwaiter();
            }

            for(int i = 0; i < numMaxParallelRequests; i++)
            {
                waiters[i].GetResult();
            }
        }
    }
}
