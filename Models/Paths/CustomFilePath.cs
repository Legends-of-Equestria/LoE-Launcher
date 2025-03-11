using Models.Utils;
namespace Models.Paths;

public class CustomFilePath(string path) : IRelativeFilePath, IAbsoluteFilePath
{
    public string Path { get; } = NormalizePath(path);

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
    
    /// <summary>
    /// Normalizes path separators based on current platform
    /// </summary>
    private static string NormalizePath(string inputPath)
    {
        var result = inputPath.Replace("./", "").Replace(".\\", "");
        return result.Replace('\\', System.IO.Path.DirectorySeparatorChar);
    }
}