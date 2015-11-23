using Microsoft.VisualStudio.TestTools.UnitTesting;
using FileUploads;
using System;
using System.IO;
using System.Collections.Generic;

namespace UTFileServer
{
    [TestClass]
    public class UTFileStore
    {
        static string fileStoreDestination = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "\\test-store";
        static int nFile = 0;

        [TestInitialize]
        public void Initialize()
        {
            Directory.CreateDirectory(fileStoreDestination);
            string[] dirs = Directory.GetDirectories(fileStoreDestination);
            Assert.AreEqual(0, dirs.Length);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // Clean the fileStoreDestination
            foreach (string dirpath in Directory.EnumerateDirectories(fileStoreDestination))
            {
                Directory.Delete(dirpath, true);
            }
        }

        [TestMethod]
        public void TestBaseStoreCreated()
        {
            FileStore fStore = new FileStore(3, fileStoreDestination);
            string[] dirs = Directory.GetDirectories(fileStoreDestination);
            Assert.AreEqual(1, dirs.Length);
        }

        private string MakeFile(string contents)
        {
            string filename = String.Format("{0}.txt", nFile);
            nFile += 1;
            File.WriteAllText(filename, contents);
            return filename;
        }

        [TestMethod]
        public void TestAddFile()
        {
            FileStore fStore = new FileStore(3, fileStoreDestination);
            string contents = "this is my text1.";
            string filename = MakeFile(contents);

            fStore.AddFile(filename);

            string[] dirs = Directory.GetDirectories(fileStoreDestination);
            Assert.AreEqual(1, dirs.Length);

            string[] files = Directory.GetFiles(dirs[0]);
            Assert.AreEqual(1, files.Length);

            string addedFileContents = File.ReadAllText(files[0]);
            Assert.AreEqual(contents, addedFileContents);
        }

        [TestMethod]
        public void TestChangeRoot()
        {
            FileStore fStore = new FileStore(3, fileStoreDestination);
            string contents = "this is my text2.";
            string filename = MakeFile(contents);

            fStore.AddFile(filename);

            string[] dirs = Directory.GetDirectories(fileStoreDestination);
            Assert.AreEqual(1, dirs.Length);

            string[] files = Directory.GetFiles(dirs[0]);
            Assert.AreEqual(1, files.Length);

            filename = MakeFile(contents);
            fStore.AddFile(filename);
            filename = MakeFile(contents);
            fStore.AddFile(filename);

            files = Directory.GetFiles(dirs[0]);
            Assert.AreEqual(3, files.Length);

            filename = MakeFile(contents);
            fStore.AddFile(filename);

            // This call should have extended the tree height by 1
            // This means a new root with 2 children and the files are in the children
            dirs = Directory.GetDirectories(fileStoreDestination);
            Assert.AreEqual(1, dirs.Length);

            dirs = Directory.GetDirectories(dirs[0]);
            Assert.AreEqual(2, dirs.Length);

            int numFiles = 0;
            foreach(string dir in dirs)
            {
                files = Directory.GetFiles(dir);
                numFiles += files.Length;
            }

            Assert.AreEqual(4, numFiles);
        }

        List<List<string>> DirSearch(string sDir, List<string> path)
        {
            List<List<string>> files = new List<List<string>>();
            path.Add(sDir);

            foreach (string f in Directory.GetFiles(sDir))
            {
                List<string> fpath = new List<string>(path);
                fpath.Add(f);
                files.Add(fpath);
            }

            foreach (string d in Directory.GetDirectories(sDir))
            {
                foreach (List<string> f in DirSearch(d, path))
                {
                    files.Add(f);
                }
            }

            path.RemoveAt(path.Count - 1);
            return files;
        }

        [TestMethod]
        public void TestAddManyFiles()
        {
            int b = 3;
            FileStore fStore = new FileStore(b, fileStoreDestination);
            string contents = "this is my text3.";
            string filename = MakeFile(contents);
            int numFiles = 89;
            int depth = (int) Math.Ceiling(Math.Log(numFiles, b)) + 2;

            for (int i=0; i<numFiles; i++)
            {
                filename = MakeFile(contents);
                fStore.AddFile(filename);
            }

            List<string> path = new List<string>();
            List<List<string>> files = DirSearch(fileStoreDestination, path);

            Assert.AreEqual(numFiles, files.Count);
            foreach (List<string> p in files)
            {
                Assert.AreEqual(depth, p.Count);
            }
        }
    }
}
