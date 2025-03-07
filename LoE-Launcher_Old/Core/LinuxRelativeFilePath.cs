using System;
using NDepend.Path;

namespace LoE_Launcher.Core
{
    public class LinuxRelativeFilePath : LinuxFilePath, IRelativeFilePath
    {
        public IRelativeFilePath GetBrotherFileWithName(string fileName)
        {
            throw new NotImplementedException();
        }

        public IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName)
        {
            throw new NotImplementedException();
        }

        public IRelativeFilePath UpdateExtension(string newExtension)
        {
            throw new NotImplementedException();
        }

        public IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
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