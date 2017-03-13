using System;
using System.IO;

namespace LoE_Launcher.Core
{
    public class CustomFilePath : IRelativeFilePath, IAbsoluteFilePath
    {
        public CustomFilePath(string path)
        {

            Path = path.Replace(".\\", "").Replace('\\',System.IO.Path.DirectorySeparatorChar);
            //Console.WriteLine(Path);
        }
        public string Path { get; }

        public string FileName => System.IO.Path.GetFileName(Path);

        IAbsoluteDirectoryPath IAbsoluteFilePath.ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path));
        public FileInfo FileInfo => new FileInfo(Path);

        public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);

        public IAbsoluteFilePath GetBrotherFileWithName(string fileName)
        {
            return new CustomFilePath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path),fileName));
        }

        public IRelativeDirectoryPath ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path));

        public IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath gameInstallFolder)
        {
            return System.IO.Path.Combine(gameInstallFolder.Path, Path).ToAbsoluteFilePathAuto();
        }

        IRelativeFilePath IRelativeFilePath.GetBrotherFileWithName(string fileName)
        {
            return new CustomFilePath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), fileName));
        }

        public bool Exists => System.IO.File.Exists(Path);

        public override string ToString()
        {
            return Path;
        }
    }
}