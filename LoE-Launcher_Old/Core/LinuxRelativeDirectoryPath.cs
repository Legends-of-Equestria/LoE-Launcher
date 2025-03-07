using System;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxRelativeDirectoryPath : LinuxDirectoryPath, IRelativeDirectoryPath
    {
        public LinuxRelativeDirectoryPath(string path)
        {
            
        }

        public IRelativeFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IRelativeFilePath GetChildFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IRelativeDirectoryPath GetChildDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteDirectoryPath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            throw new NotImplementedException();
        }

        IAbsolutePath IRelativePath.GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            return GetAbsolutePathFrom(pivotDirectory);
        }

        public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
        {
            throw new NotImplementedException();
        }

        public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureReason)
        {
            throw new NotImplementedException();
        }

        public IRelativeDirectoryPath ParentDirectoryPath { get; }
    }
}