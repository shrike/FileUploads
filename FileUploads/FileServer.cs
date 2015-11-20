using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace FileUploads
{
    public class FileServer
    {
        HttpListener listener;
        int numMaxParallelRequests;
        int concurrentAllowedRequests;
        string fileUploadsDestination;

        public FileServer(string pref, int maxParallelRequests,
            string destination)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(pref);
            numMaxParallelRequests = maxParallelRequests;
            concurrentAllowedRequests = numMaxParallelRequests;
            fileUploadsDestination = destination;
            bool exists = System.IO.Directory.Exists(fileUploadsDestination);
            if (exists == false)
            {
                System.IO.Directory.CreateDirectory(fileUploadsDestination);
            }
        }

        private void StoreFileFromRequest(HttpListenerRequest request)
        {
            if (request.ContentType.StartsWith("multipart/form-data") == false)
            {
                return;
            }

            System.IO.Stream body = request.InputStream;
            System.Text.Encoding encoding = request.ContentEncoding;
            System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);

            // This is not the proper way to parse a multipart request,
            // but for the sake of simplicity...
            string boundary = reader.ReadLine();
            string line = reader.ReadLine();
            while (line.Length != 0)
            {
                line = reader.ReadLine();
            }

            System.IO.StreamWriter file = new System.IO.StreamWriter(fileUploadsDestination + "\\test1.txt");
            string prevLine = null;
            while (reader.EndOfStream == false)
            {
                line = reader.ReadLine();
                if (line.StartsWith(boundary) == false)
                {
                    if (prevLine != null)
                    {
                        file.WriteLine(prevLine);
                    }
                }
                else
                {
                    file.Write(prevLine);
                }
                prevLine = line;
            }

            file.Close();
            body.Close();
            reader.Close();
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
                        System.IO.Stream output = response.OutputStream;
                        Thread.Sleep(5000);
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
            for (int i = 0; i < numMaxParallelRequests+1; i++)
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
