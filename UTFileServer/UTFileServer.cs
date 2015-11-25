using System.Net;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System.IO;
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

            server = new FileUploads.FileServer(pref, numMaxParallelRequests, fileUploadDestination, 3);
            server.Start();
        }

        [TestCleanup]
        public void CleanUp()
        {
            server.Stop();
            cleanUploads();
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
            foreach (string dirpath in Directory.EnumerateDirectories(fileUploadDestination))
            {
                Directory.Delete(dirpath, true);
            }

            string[] files = Directory.GetFiles(fileUploadDestination);
            Assert.AreEqual(0, files.Length);
            string[] dirs = Directory.GetDirectories(fileUploadDestination);
            Assert.AreEqual(0, dirs.Length);
        }

        public HttpStatusCode FileUpload(string filename)
        {
            WebClient webClient = new WebClient();
            byte[] responseArray;
            try
            {
                responseArray = webClient.UploadFile(pref, filename);
            }
            catch (WebException e)
            {
                var r = (HttpWebResponse) e.Response;
                return r.StatusCode;
            }
            string res = System.Text.Encoding.ASCII.GetString(responseArray);

            return HttpStatusCode.OK;
        }

        public HttpStatusCode FileUpload(string filename, string fileContent)
        {
            File.WriteAllText(filename, fileContent);
            HttpStatusCode statusCode = FileUpload(filename);

            // Make sure that a single dir with a single file was created
            string[] dirs = Directory.GetDirectories(fileUploadDestination);
            Assert.AreEqual(1, dirs.Length);
            string[] files = Directory.GetFiles(dirs[0]);
            // Make sure the contents of the file are the same
            string uploadedFileContent = File.ReadAllText(files[0]);
            Assert.AreEqual(fileContent.Length, uploadedFileContent.Length);
            Assert.AreEqual(fileContent, uploadedFileContent);
            return statusCode;
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
            StreamWriter file = new StreamWriter(filename);

            for (int i=0; i<10000000; ++i)
            {
                file.WriteLine("Here is line {0}.", i);
            }
            file.Close();
            FileUpload(filename);
        }

        [TestMethod]
        public void TestParallelFileUploads()
        {
            Task<HttpStatusCode>[] tasks = new Task<HttpStatusCode>[numMaxParallelRequests];

            for (int i = 0; i < numMaxParallelRequests; i++)
            {
                var j = i + 1;
                tasks[i] = Task.Run(() =>
                {
                    string filename = String.Format("test-file-{0}-of-{1}", j, numMaxParallelRequests);
                    string fileContent = String.Format("Contents of file {0}. End.", j);
                    File.WriteAllText(filename, fileContent);
                    return FileUpload(filename);
                });
            }

            int numGoodResponses = 0;
            for (int i = 0; i < numMaxParallelRequests; i++)
            {
                HttpStatusCode statusCode = tasks[i].Result;
                if (statusCode == HttpStatusCode.OK)
                {
                    numGoodResponses += 1;
                }
            }

            Assert.AreEqual(numMaxParallelRequests, numGoodResponses);
        }
    } // public class UTFileServer
} // namespace UTFileServer
