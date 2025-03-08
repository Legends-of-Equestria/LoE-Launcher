namespace LoE_Launcher.Core.Models.Paths;

public interface IRelativeDirectoryPath
{
    IAbsoluteDirectoryPath GetAbsolutePathFrom(IAbsoluteDirectoryPath launcherPath);
    string Path { get; }
    IRelativeFilePath GetChildFileWithName(string fileName);
}