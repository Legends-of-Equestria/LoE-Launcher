using System;
using NDepend.Path;

namespace LoE_Launcher.Core
{

    public abstract class LinuxPath : IPath
    {
        public bool IsChildOf(IDirectoryPath parentDirectory)
        {
            throw new NotImplementedException();
        }

        public bool NotEquals(object obj)
        {
            throw new NotImplementedException();
        }

        public bool IsAbsolutePath { get; }
        public bool IsRelativePath { get; }
        public bool IsEnvVarPath { get; }
        public bool IsVariablePath { get; }
        public bool IsDirectoryPath { get; }
        public bool IsFilePath { get; }
        public PathMode PathMode { get; }
        public IDirectoryPath ParentDirectoryPath { get; }
        public bool HasParentDirectory { get; }
    }
}