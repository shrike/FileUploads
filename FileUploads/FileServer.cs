using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace FileUploads
{
    // This parser comes from
    // http://stackoverflow.com/questions/8466703/httplistener-and-file-upload
    // Thank you, Paul Wheeler, for this, since apparently no good MS implementation exists
    // for this particular problem. Or at least I could not find one...
    static class MultipartParser
    {
        private static string GetBoundary(string ctype)
        {
            return "--" + ctype.Split(';')[1].Split('=')[1];
        }

        public static void SaveFile(Encoding enc, string contentType, Stream input, string filepath)
        {
            string boundary = GetBoundary(contentType);
            byte[] boundaryBytes = enc.GetBytes(boundary);
            int boundaryLen = boundaryBytes.Length;

            using (FileStream output = new FileStream(filepath, FileMode.Create, FileAccess.Write))
            {
                byte[] buffer = new byte[1024];
                int len = input.Read(buffer, 0, 1024);
                int startPos = -1;

                // Find start boundary
                while (true)
                {
                    if (len == 0)
                    {
                        throw new Exception("Start Boundaray Not Found");
                    }

                    startPos = IndexOf(buffer, len, boundaryBytes);
                    if (startPos >= 0)
                    {
                        break;
                    }
                    else
                    {
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen);
                    }
                }

                // Skip four lines (Boundary, Content-Disposition, Content-Type, and a blank)
                for (Int32 i = 0; i < 4; i++)
                {
                    while (true)
                    {
                        if (len == 0)
                        {
                            throw new Exception("Preamble not Found.");
                        }

                        startPos = Array.IndexOf(buffer, enc.GetBytes("\n")[0], startPos);
                        if (startPos >= 0)
                        {
                            startPos++;
                            break;
                        }
                        else
                        {
                            len = input.Read(buffer, 0, 1024);
                        }
                    }
                }

                Array.Copy(buffer, startPos, buffer, 0, len - startPos);
                len = len - startPos;

                while (true)
                {
                    Int32 endPos = IndexOf(buffer, len, boundaryBytes);
                    endPos -= 2; // we need to skip the last /r/n that comes before the boundary - it is not part of the original data
                    if (endPos >= 0)
                    {
                        if (endPos > 0) output.Write(buffer, 0, endPos);
                        break;
                    }
                    else if (len <= boundaryLen)
                    {
                        throw new Exception("End Boundaray Not Found");
                    }
                    else
                    {
                        output.Write(buffer, 0, len - boundaryLen);
                        Array.Copy(buffer, len - boundaryLen, buffer, 0, boundaryLen);
                        len = input.Read(buffer, boundaryLen, 1024 - boundaryLen) + boundaryLen;
                    }
                }
            }
        }

        private static Int32 IndexOf(Byte[] buffer, Int32 len, Byte[] boundaryBytes)
        {
            for (Int32 i = 0; i <= len - boundaryBytes.Length; i++)
            {
                Boolean match = true;
                for (Int32 j = 0; j < boundaryBytes.Length && match; j++)
                {
                    match = buffer[i + j] == boundaryBytes[j];
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

    }


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

        private void StoreFileFromRequest(HttpListenerRequest request)
        {
            if (request.ContentType == null || request.ContentType.StartsWith("multipart/form-data") == false)
            {
                return;
            }

            string filePath = Path.Combine(fileUploadsDestination, Path.GetRandomFileName());
            MultipartParser.SaveFile(request.ContentEncoding, request.ContentType, request.InputStream, filePath);

            // The fileStore cannot be used concurrently
            lock (fileStore)
            {
                fileStore.AddFile(filePath);
            }
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
                        // Uncomment when testing empty parallel requests in order to simulate some work
                        //Thread.Sleep(2000);
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
