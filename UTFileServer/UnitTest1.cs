using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UTFileServer
{
    [TestClass]
    public class UnitTest1
    {
        static string pref = "http://localhost:8080/";
        static FileUploads.FileServer server;

        [ClassInitialize]
        public static void Initialize(TestContext ctx)
        {
            server = new FileUploads.FileServer(pref);
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
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pref);
            req.Timeout = 1000;
            req.GetResponse();
        }

        [TestMethod]
        public void TestConsequtiveRequest()
        {
            TestSingleRequest();
            TestSingleRequest();
            TestSingleRequest();
            TestSingleRequest();
        }
    }
}
