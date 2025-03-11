using System.Diagnostics;
using Models.Utils;
namespace Models.Paths;

[method: DebuggerStepThrough]
public class CustomDirectoryPath(string path) : IRelativeDirectoryPath, IAbsoluteDirectoryPath
{
    public string Path { get; } = NormalizePath(path);
     
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
    
    /// <summary>
    /// Normalizes path separators based on current platform
    /// </summary>
    private static string NormalizePath(string inputPath)
    {
        // Handle null or empty path
        if (string.IsNullOrEmpty(inputPath))
        {
            return string.Empty;
        }
        
        // Remove relative path prefix if present
        var result = inputPath.Replace("./", "").Replace(".\\", "");
        
        // Replace Windows backslashes with the platform-specific directory separator
        return result.Replace('\\', System.IO.Path.DirectorySeparatorChar);
    }
}