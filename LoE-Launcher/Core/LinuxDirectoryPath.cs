using System;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxDirectoryPath : LinuxPath, IDirectoryPath
    {
        public IFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IFilePath GetChildFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IDirectoryPath GetChildDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public string DirectoryName { get; }
    }
}