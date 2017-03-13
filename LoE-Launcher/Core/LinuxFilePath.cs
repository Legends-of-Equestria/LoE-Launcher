using System;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxFilePath : LinuxPath, IFilePath
    {
        public bool HasExtension(string extension)
        {
            throw new NotImplementedException();
        }

        public IFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IFilePath UpdateExtension(string newExtension)
        {
            throw new NotImplementedException();
        }

        public string FileName { get; }
        public string FileNameWithoutExtension { get; }
        public string FileExtension { get; }
    }
}