1. Installation

You can start by unpacking the attached archive or cloning the repo from github (). Either way you will end up with a clean git repo containing the Visual Studio solution with both the server and the client. Next, open the solution in MS Visual Studio Enterprise 2015. I used this particular (trial) version and am not sure whether the solution will open properly in other versions of VS. Once opened, install the dependencies via NuGet. The solution should contain 2 windows console applications - FileServer and ParallelUploader. Build both of them (debug/release is up you I guess). Then, navigate to the output dir for the server (FileUploads\FileUploads\bin\Release) and execute the .exe file found there (FileUploads.exe). This should start an HTTP server on localhost:8080 with a default uploads dir C:\uploads. If you wish to use a different directory to store the uploads pass it as a parameter:

  FileUploads.exe c:\different-uploads-dir
  
 Finally, navigate to the output dir for the client (FileUploads\ParallelUploader\bin\Release) and execute the .exe there (ParallelUploader.exe) passing it the dir containing the files you wish to upload:

 ParallelUploader.exe c:\files-to-upload
 
 Note that files from all child dirs will also be targeted for uploading (and deleting upon successful uploading - passing c:\ as a parameter is A VERY BAD IDEA; you've been warned).

 
2. Server-side mechanism for limiting the maximum number of concurrent file uploads

I spent a considerable time familiarizing myself with c# threads and the concurrency primitives offered. I decided to use Tasks for processing requests concurrently and a simple Interlocked.decrement + an if statement for limiting the maximum number of concurrent requests. At any point in time there are 17 tasks executing (actually that number is numMaxParallelRequests + 1). The program also starts up with a counter named concurrentAllowedRequests = 16 (actually to numMaxParallelRequests). Every time a task starts to process a request, concurrentAllowedRequests is decremented atomically and if the resulting value (NOT the value of concurrentAllowedRequests) is less than 0 than we are already processing the max number of requests and we return HTTP status code 503. When we are done with a request, we atomically increment the concurrentAllowedRequests counter to allow a new request to be processed.


2a. Do you think this can be implemented in a different way (other than using the C# concurrency primitives)?

We could accomplish this same tasks using 16+1 single-thread processes where one main process receives the requests and hands them off for processing to any free worker of the remaining 16, and responding with HTTP status code 503 when all workers are busy.


3. Server-side mechanism for storing large numbers of files efficiently

My solution for storing a large number of files is to spread the files over a number of directories. The number of directories in any given dir is also limited. I basically build a simple tree out of directories where each node is limited in the number of children it has. I call this limit "branchingFactor". My simple tree has one additional property and that is that all files are stored in the leaf directories and are all at the same depth of the tree. I will give a few examples.

Suppose branchingFactor = 3. FileStore starts by creating a root dir with a random name. I will call it root1 here. Each time a file is added to the FileStore, the file is placed directly in root1, until there are 3 files there:

root1\
  file1
  file2
  file3
  
When the next file is added to the FileStore, I know that I have no more space for files because I've counted that I've stored 3 files in a tree of height 1 (max files = branchingFactor ^ height). FileStore then creates a new root for the tree, I'll call it root2 and moves the old root into the new one:

root2\
  root1\
    file1
    file2
    file3

Now, we have space for up to 9 files. We add the 4th file to a new branch of the new root:

root2\
  root1\
    file1
    file2
    file3
  new_dir\
    file4
	
Similarly, when this tree ot height/depth 2 fills up, I create a new root and move root2 into it thus opening up space for more files.


3a. Are there any concurrency issues related to your proposed mechanism?

The tree I implemented cannot be used in a concurrent manner, so I have to lock on it every time I add a file. However, the operations in the tree are relatively quick - there's no reading/writing of file data - only moving files and dirs around which is a quick operation.


3b. Assume that you want to store the files on more than one file system, e.g. separate partitions or physical disks. Would your mechanism work in this scenario? Can you suggest improvements (implementation is not required)?

The mechanism I have in place now will not work for more than one parent dir, however, more than one tree can be instantiated for each parent dir we want to store files in. Moreover, if we want to use separate partitions or physical disks to store files in this manner, a more appropriate solution would be to use a virtual volume which spans over all the dirs/partitions/disks we want to use as storage, rather than attempting to solve this in our file upload server.


4. How would you implement the server application in JavaScript (implementation is not required)?

Since node.js (which is what I would use to build such a file upload server) is inherently a single-threaded environment, I would use a main process + a number of child processes in a similar manner to what I describe earlier in "Do you think this can be implemented in a different way (other than using the C# concurrency primitives)?". More specifically, I would use the cluster module: https://nodejs.org/api/cluster.html


5. Client-side mechanism for uploading files concurrently

I enumerate the target dir files and using Parallel.ForEach assign them to a number of threads to be uploaded to the server. If any thread gets a 503 from the server, it waits a bit and retries the upload until it succeeds or fails with a different status code.


5a. Explain why you believe your solution is the most efficient and what factors 
could limit its performance. Statistics is the best evidence (unless you have a
mathematical proof, which is difficult in this particular case).

My solution is efficient in that it keeps the server as busy as possible by always uploading as many files concurrently as the server would allow. Some factors that could limit the performance of the client as I have implemented it are disk read speed and file positions on disk. Basically, if the client is trying to upload 2 files concurrently over a very fast network (or to a server running on the same machine as the client which is the case when I was testing) and assuming the 2 files are on the same disk, then disk read will limit our speed. Moreover, uploading a single file in this case could possibly be faster since the disk would spend all its time reading and no time will be wasted in rotating the disk between the locations of the 2 files.


Would you say your solution is robust? Why or why not?

It is not very robust because both the server and the client have very little code in the way of error handling and thus WILL crash when used improperly.


How would you improve your solution if you had more time?

Error handling. I've written very little in the way of exception handling and consequently the server and client would sometimes crash with an unhandled exception instead of exiting gracefully or printing a warning and continuing execution. I also would invest more time in parsing the http multipart request as I cannot currently upload BIG files. Also the tests I wrote were primarily used to help me in development and leave a lot to be desired. Finally, much of the constants I use in both the server and the client could be exposed as configuration or command line parameters.


How do you rate the complexity of this test task on the scale of 1 to 5?

I would say it was a 4 for me as test tasks are concerned. I had to do quite a bit of reading and researching to get into C#, to learn its threading and synchronization, and to find the building blocks I needed to build a simple web server. However, the task was not so complex as to frustrate me and I rather enjoyed working on it.