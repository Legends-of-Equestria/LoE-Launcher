using System;
using System.IO;
using NDepend.Helpers;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxAbsoluteDirectoryPath : LinuxDirectoryPath, IAbsoluteDirectoryPath
    {
        public bool OnSameVolumeThan(IAbsolutePath pathAbsoluteOther)
        {
            throw new NotImplementedException();
        }

        IRelativePath IAbsolutePath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            return GetRelativePathFrom(pivotDirectory);
        }

        public IRelativeDirectoryPath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            throw new NotImplementedException();
        }

        public DirectoryInfo DirectoryInfo { get; }
        public IReadOnlyList<IAbsoluteFilePath> ChildrenFilesPath { get; }
        public IReadOnlyList<IAbsoluteDirectoryPath> ChildrenDirectoriesPath { get; }

        public IAbsoluteFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteFilePath GetChildFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteDirectoryPath GetChildDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
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