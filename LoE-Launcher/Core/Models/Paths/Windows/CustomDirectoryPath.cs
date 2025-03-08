using System.Diagnostics;
using LoE_Launcher.Core.Utils;
namespace LoE_Launcher.Core.Models.Paths.Windows;

[method: DebuggerStepThrough]
public class CustomDirectoryPath(string path) : IRelativeDirectoryPath, IAbsoluteDirectoryPath
{
    public string Path { get; } = path.Replace(".\\", "").Replace('\\', System.IO.Path.DirectorySeparatorChar);
     
    public DirectoryInfo DirectoryInfo => new(Path);
    public string DirectoryName => System.IO.Path.GetFileName(Path);
    public IAbsoluteDirectoryPath ParentDirectoryPath => new CustomDirectoryPath(System.IO.Path.GetDirectoryName(Path) ?? string.Empty);
        
    [DebuggerStepThrough]
    IRelativeFilePath IRelativeDirectoryPath.GetChildFileWithName(string fileName)
    {
        return new CustomFilePath(System.IO.Path.Combine(Path, fileName));
    }
        
    public bool Exists => System.IO.Directory.Exists(Path);
        
    [DebuggerStepThrough]
    IAbsoluteFilePath IAbsoluteDirectoryPath.GetChildFileWithName(string fileName)
    {
        return new CustomFilePath(System.IO.Path.Combine(Path, fileName));
    }
        
    [DebuggerStepThrough]
    public IAbsoluteDirectoryPath GetAbsolutePathFrom(IAbsoluteDirectoryPath launcherPath)
    {
        return System.IO.Path.Combine(launcherPath.Path, Path).ToAbsoluteDirectoryPathAuto();
    }
        
    public override string ToString() => Path;
}