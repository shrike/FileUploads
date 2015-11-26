using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace FileUploads
{
    public class FileServer
    {
        HttpListener listener;
        int numMaxParallelRequests;
        int concurrentAllowedRequests;
        string fileUploadsDestination;
        FileStore fileStore;

        public FileServer(string pref, int maxParallelRequests,
            string destination, int numMaxDirNodes=50)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(pref);
            numMaxParallelRequests = maxParallelRequests;
            concurrentAllowedRequests = numMaxParallelRequests;
            fileUploadsDestination = destination;
            Directory.CreateDirectory(fileUploadsDestination);
            fileStore = new FileStore(numMaxDirNodes, fileUploadsDestination);
        }

        private async void StoreFileFromRequest(HttpListenerRequest request)
        {
            if (request.ContentType == null || request.ContentType.StartsWith("multipart/form-data") == false)
            {
                return;
            }

            Stream body = request.InputStream;

            var streamContent = new StreamContent(body);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);

            var provider = await streamContent.ReadAsMultipartAsync();

            foreach (var httpContent in provider.Contents)
            {
                var fileName = httpContent.Headers.ContentDisposition.FileName;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                using (Stream fileContents = await httpContent.ReadAsStreamAsync())
                {
                    string filePath = Path.Combine(fileUploadsDestination, Path.GetRandomFileName());
                    FileStream file = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    fileContents.CopyTo(file);
                    file.Close();

                    // The fileStore cannot be used concurrently
                    lock (fileStore)
                    {
                        fileStore.AddFile(filePath);
                    }
                }
            }

            body.Close();
        }

        private void ProcessRequest()
        {
            Task<HttpListenerContext> getContextTask = listener.GetContextAsync();
            Task processRequestTask = getContextTask.ContinueWith((res) =>
            {
                var context = res.Result as HttpListenerContext;
                try
                {
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    if (Interlocked.Decrement(ref concurrentAllowedRequests) < 0)
                    {
                        response.StatusCode = 503;
                    }
                    else
                    {
                        string responseString = "<html><body>File uploaded successfully.</body></html>";
                        StoreFileFromRequest(request);
                        byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        Stream output = response.OutputStream;
                        Thread.Sleep(1000);
                        output.Write(buffer, 0, buffer.Length);
                    }
                    Interlocked.Increment(ref concurrentAllowedRequests);
                }
                finally
                {
                    context.Response.OutputStream.Close();
                }
                // When we are done processing this task, start on the next
                ProcessRequest();
            });
        }

        public void Start()
        {
            listener.Start();
            // We start one more thread that listens for requests whose
            // sole purpose will be to respond with 503 when all other
            // request processors are busy.
            for (int i = 0; i < numMaxParallelRequests + 1; i++)
            {
                ProcessRequest();
            }
        }

        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }
    }
}
