using System;
using System.Collections.Generic;
using System.IO;

namespace FileUploads
{
    public class DirStoreFullException : System.Exception
    {

    }

    public class DirStore
    {
        int branchingFactor;
        string basePath;
        string name;
        List<string> files;
        List<DirStore> children;
        DirStore parent;

        public DirStore(int branchingFactor, string basePath)
        {
            this.branchingFactor = branchingFactor;
            this.basePath = basePath;
            name = Path.GetRandomFileName();
            Directory.CreateDirectory(Path.Combine(basePath, name));
            files = new List<string>();
            children = new List<DirStore>();
            parent = null;
        }

        public string GetPath()
        {
            if (parent == null)
            {
                return Path.Combine(basePath, name);
            }

            return Path.Combine(parent.GetPath(), name);
        }

        private bool HasSpaceForFile()
        {
            return files.Count < branchingFactor;
        }

        public void AddFile(string filepath, int atDepth)
        {
            if (atDepth == 0)
            {
                if (HasSpaceForFile())
                {
                    files.Add(filepath);
                    string path = GetPath();
                    File.Move(filepath, Path.Combine(path, Path.GetFileName(filepath)));
                }
            }
            else
            {
                if (children.Count > 0)
                {
                    // If there is any space available it will be in the child last added
                    DirStore lastChild = children[children.Count - 1];
                    if (atDepth > 1 || lastChild.HasSpaceForFile())
                    {
                        lastChild.AddFile(filepath, atDepth - 1);
                        return;
                    }
                }

                // If we get to here then all currently available places for files are full.
                // Our last option is to create a new child.
                DirStore newChild = new DirStore(branchingFactor, basePath);
                newChild.AddFile(filepath, atDepth - 1);
                AddDirStore(newChild);
            }
        }

        public void AddDirStore(DirStore store)
        {
            if (children.Count < branchingFactor)
            {
                children.Add(store);
                string oldPath = store.GetPath();
                store.parent = this;
                string newPath = store.GetPath();
                try
                {
                    Directory.Move(oldPath, newPath);
                }
                catch
                {
                    // For some reason, the above throws an access violation SOMETIMES.
                    // I think this happens when the directory was just created.
                    // Retrying the operation here seams to eliminate all problems.
                    Directory.Move(oldPath, newPath);
                }
            }
            else
            {
                if (parent == null)
                {
                    throw new DirStoreFullException();
                }
                else
                {
                    DirStore prevStore = new DirStore(branchingFactor, basePath);
                    prevStore.AddDirStore(store);
                    parent.AddDirStore(prevStore);
                }
            }
        }
    }

    public class FileStore
    {
        int branchingFactor;
        int numFiles;
        DirStore root;
        int height;
        string basePath;

        public FileStore(int branchingFactor, string basePath)
        {
            this.branchingFactor = branchingFactor;
            this.basePath = basePath;
            numFiles = 0;
            height = 1;
            root = new DirStore(branchingFactor, basePath);
        }

        private bool IsSpaceAvailable()
        {
            return Math.Pow(branchingFactor, height) > numFiles;
        }

        public void AddFile(string filepath)
        {
            if (IsSpaceAvailable())
            {
                root.AddFile(filepath, height - 1);
                numFiles += 1;
            }
            else
            {
                // No space is available in the current tree -- add a new level
                DirStore new_root = new DirStore(branchingFactor, basePath);
                new_root.AddDirStore(root);
                root = new_root;
                height += 1;
                AddFile(filepath);
            }
        }
    }
}
