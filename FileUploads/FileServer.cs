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

        public FileServer(string pref, int maxParallelRequests)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(pref);
            numMaxParallelRequests = maxParallelRequests;
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
                    string responseString = "<HTML><BODY> Hello world!</BODY></HTML>";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    Thread.Sleep(500);
                    output.Write(buffer, 0, buffer.Length);
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
            for (int i = 0; i < numMaxParallelRequests; i++)
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
