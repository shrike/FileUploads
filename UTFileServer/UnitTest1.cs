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
        const int additionalRequests = 2;

        [ClassInitialize]
        public static void Initialize(TestContext ctx)
        {
            // We need at least numMaxParallelRequests to process that many
            // incoming requests and we need numMaxParallelRequests more threads
            // to be able to issue that many requests in parallel.
            ThreadPool.SetMinThreads(numMaxParallelRequests*2+additionalRequests, 0);

            server = new FileUploads.FileServer(pref, numMaxParallelRequests);
            server.Start();
        }

        [ClassCleanup]
        public static void CleanUp()
        {
            server.Stop();
        }

        private HttpWebResponse MakeSingleRequest()
        {
            HttpWebResponse res = null;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pref);
            req.Timeout = 6000;
            try
            {
                res = (HttpWebResponse)req.GetResponse();
                res.Close();
            }
            catch (WebException e)
            {
                res = (HttpWebResponse) e.Response;
            }
            return res;
        }

        [TestMethod]
        public void TestSingleRequest()
        {
            MakeSingleRequest();
        }

        [TestMethod]
        public void TestConsecutiveRequest()
        {
            MakeSingleRequest();
            MakeSingleRequest();
            MakeSingleRequest();
            MakeSingleRequest();
        }

        [TestMethod]
        public void TestParallelRequests()
        {
            Task<HttpWebResponse>[] tasks = new Task<HttpWebResponse>[numMaxParallelRequests];

            for (int i = 0; i < numMaxParallelRequests; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    return MakeSingleRequest();
                });
            }

            int numGoodResponses = 0;
            for (int i = 0; i < numMaxParallelRequests; i++)
            {
                HttpWebResponse res = tasks[i].Result;
                if (res.StatusCode == HttpStatusCode.OK)
                {
                    numGoodResponses += 1;
                }
            }

            Assert.AreEqual(numMaxParallelRequests, numGoodResponses);
        }

        [TestMethod]
        public void TestTooManyParallelRequests()
        {
            Task<HttpWebResponse>[] tasks= new Task<HttpWebResponse>[numMaxParallelRequests+additionalRequests];

            for (int i = 0; i < numMaxParallelRequests + additionalRequests; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    return MakeSingleRequest();
                });
            }

            int numBadResponses = 0;
            int numGoodResponses = 0;
            for (int i = 0; i < numMaxParallelRequests + additionalRequests; i++)
            {
                HttpWebResponse res = tasks[i].Result;
                if (res.StatusCode == HttpStatusCode.ServiceUnavailable)
                {
                    numBadResponses += 1;
                }
                else if (res.StatusCode == HttpStatusCode.OK)
                {
                    numGoodResponses += 1;
                }
            }

            Assert.AreEqual(additionalRequests, numBadResponses);
            Assert.AreEqual(numMaxParallelRequests, numGoodResponses);
        }
    }
}
