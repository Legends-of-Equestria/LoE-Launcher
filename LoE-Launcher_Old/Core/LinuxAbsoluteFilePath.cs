using System;
using System.IO;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxAbsoluteFilePath : LinuxFilePath, IAbsoluteFilePath
    {
        public bool OnSameVolumeThan(IAbsolutePath pathAbsoluteOther)
        {
            throw new NotImplementedException();
        }

        public IRelativeFilePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteFilePath UpdateExtension(string newExtension)
        {
            throw new NotImplementedException();
        }

        public FileInfo FileInfo { get; }

        IRelativePath IAbsolutePath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            return GetRelativePathFrom(pivotDirectory);
        }

        public bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            throw new NotImplementedException();
        }

        public bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureReason)
        {
            throw new NotImplementedException();
        }

        public AbsolutePathKind Kind { get; }
        public IDriveLetter DriveLetter { get; }
        public string UNCServer { get; }
        public string UNCShare { get; }
        public bool Exists { get; }
        public IAbsoluteDirectoryPath ParentDirectoryPath { get; }
    }
}