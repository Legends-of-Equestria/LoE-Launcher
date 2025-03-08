using LoE_Launcher.Core.Utils;
namespace LoE_Launcher.Core.Models.Paths.Windows;

public class CustomFilePath(string path) : IRelativeFilePath, IAbsoluteFilePath
{
    public string Path { get; } = path.Replace(".\\", "").Replace('\\', System.IO.Path.DirectorySeparatorChar);

    public string FileName => System.IO.Path.GetFileName(Path);
    IAbsoluteDirectoryPath IAbsoluteFilePath.ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path) ?? string.Empty);
    public FileInfo FileInfo => new(Path);
    public string FileNameWithoutExtension => System.IO.Path.GetFileNameWithoutExtension(Path);
        
    public IAbsoluteFilePath GetBrotherFileWithName(string fileName)
    {
        return new CustomFilePath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? string.Empty, fileName));
    }
        
    public IRelativeDirectoryPath ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path) ?? string.Empty);
        
    public IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath gameInstallFolder)
    {
        return System.IO.Path.Combine(gameInstallFolder.Path, Path).ToAbsoluteFilePathAuto();
    }
        
    IRelativeFilePath IRelativeFilePath.GetBrotherFileWithName(string fileName)
    {
        return new CustomFilePath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path) ?? string.Empty, fileName));
    }
        
    public bool Exists => System.IO.File.Exists(Path);
        
    public override string ToString() => Path;
}