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

        public FileServer(string pref, int maxParallelRequests)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(pref);
            numMaxParallelRequests = maxParallelRequests;
            concurrentAllowedRequests = numMaxParallelRequests;
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
