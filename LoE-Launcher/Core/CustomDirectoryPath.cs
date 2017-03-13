using System;
using System.IO;

namespace LoE_Launcher.Core
{
    public class CustomDirectoryPath : IRelativeDirectoryPath, IAbsoluteDirectoryPath
    {
        public CustomDirectoryPath(string path)
        {
            Path = path.Replace(".\\", "").Replace('\\', System.IO.Path.DirectorySeparatorChar);
            //Console.WriteLine(Path);
        }

        public string Path { get; }

        public DirectoryInfo DirectoryInfo => new DirectoryInfo(Path);

        public string DirectoryName => System.IO.Path.GetFileName(Path);
        public IAbsoluteDirectoryPath ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path));

        IRelativeFilePath IRelativeDirectoryPath.GetChildFileWithName(string fileName)
        {
            return new CustomFilePath(System.IO.Path.Combine(Path, fileName));
        }

        public bool Exists => System.IO.Directory.Exists(Path);
        IAbsoluteFilePath IAbsoluteDirectoryPath.GetChildFileWithName(string fileName)
        {
            return new CustomFilePath(System.IO.Path.Combine(Path, fileName));
        }

        public IAbsoluteDirectoryPath GetAbsolutePathFrom(IAbsoluteDirectoryPath launcherPath)
        {
            return System.IO.Path.Combine(launcherPath.Path, Path).ToAbsoluteDirectoryPathAuto();
        }

        public override string ToString()
        {
            return Path;
        }
    }
}