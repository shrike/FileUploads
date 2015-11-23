using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System;

namespace UTFileServer
{
    [TestClass]
    public class UTFileServer
    {
        static string pref = "http://localhost:8080/";
        static FileUploads.FileServer server;
        const int numMaxParallelRequests = 16;
        const int additionalRequests = 2;
        static string fileUploadDestination = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "\\test-uploads";

        [TestInitialize]
        public void Initialize()
        {
            // We need at least numMaxParallelRequests to process that many
            // incoming requests and we need numMaxParallelRequests more threads
            // to be able to issue that many requests in parallel.
            ThreadPool.SetMinThreads(numMaxParallelRequests * 2 + additionalRequests, 0);

            server = new FileUploads.FileServer(pref, numMaxParallelRequests, fileUploadDestination);
            server.Start();
        }

        [TestCleanup]
        public void CleanUp()
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
                res = (HttpWebResponse)e.Response;
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
            Task<HttpWebResponse>[] tasks = new Task<HttpWebResponse>[numMaxParallelRequests + additionalRequests];

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

        private void cleanUploads()
        {
            // Remove all files from the uploads dir
            foreach (string filepath in System.IO.Directory.EnumerateFiles(fileUploadDestination))
            {
                System.IO.File.Delete(filepath);
            }
            string[] files = System.IO.Directory.GetFiles(fileUploadDestination);
            Assert.AreEqual(0, files.Length);
        }

        public void FileUpload(string filename)
        {
            WebClient webClient = new WebClient();
            byte[] responseArray = webClient.UploadFile(pref, filename);
            string res = System.Text.Encoding.ASCII.GetString(responseArray);

            // Now check that a file was created in the uploads dir
            string[] files = System.IO.Directory.GetFiles(fileUploadDestination);
            Assert.AreEqual(1, files.Length);
        }

        public void FileUpload(string filename, string fileContent)
        {
            System.IO.File.WriteAllText(filename, fileContent);
            FileUpload(filename);

            // Make sure the contents of the file are the same
            string[] files = System.IO.Directory.GetFiles(fileUploadDestination);
            string uploadedFileContent = System.IO.File.ReadAllText(files[0]);
            Assert.AreEqual(fileContent.Length, uploadedFileContent.Length);
            Assert.AreEqual(fileContent, uploadedFileContent);
        }

        [TestMethod]
        public void TestFileUpload()
        {
            string filename = "test-file1.txt";
            string fileContent = "This is some text in the test file 1. Ta-daaa. Noise!\r\nLine2\r\nThis is the end.  ";
            FileUpload(filename, fileContent);
        }

        [TestMethod]
        public void TestEmptyFileUpload()
        {
            string filename = "test-file2.txt";
            string fileContent = "";
            FileUpload(filename, fileContent);
        }

        [TestMethod]
        public void TestLargeFileUpload()
        {
            string filename = "test-file2.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(filename);

            for (int i=0; i<10000000; ++i)
            {
                file.WriteLine("Here is line {0}.", i);
            }
            file.Close();
            FileUpload(filename);
        }
    }
}
