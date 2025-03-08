namespace LoE_Launcher.Core.Models.Paths;

public interface IAbsoluteFilePath
{
    string Path { get; }
    bool Exists { get; }
    string FileName { get; }
    IAbsoluteDirectoryPath ParentDirectoryPath { get; }
    FileInfo FileInfo { get; }
    string FileNameWithoutExtension { get; }
    IAbsoluteFilePath GetBrotherFileWithName(string fileName);
}