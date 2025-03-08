namespace LoE_Launcher.Core.Models.Paths;

public interface IRelativeFilePath
{
    string Path { get; }
    string FileName { get; }
    IRelativeDirectoryPath ParentDirectoryPath { get; }
    string FileNameWithoutExtension { get; }
    IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath gameInstallFolder);
    IRelativeFilePath GetBrotherFileWithName(string fileName);
}